using System.Text.Json;
using Freuds.Core.Model;

namespace Freuds.Core.Levels;

/// <summary>
/// Reads a level exported from TileMap, which is Tiled-format JSON.
/// Terrain comes from the Collision layer, the player start and units from point objects on the
/// "Spawn Points" layer, and the art from tile objects on every other object layer.
/// </summary>
public static class TileMapImporter
{
    private const uint FlipHorizontalBit = 0x80000000u;
    private const uint FlipVerticalBit = 0x40000000u;
    private const uint FlipDiagonalBit = 0x20000000u;
    private const uint GidMask = 0x1FFFFFFFu;

    private const string SpawnLayerName = "Spawn Points";

    // Spawn point names, without the " Spawn" ending, spaces and case. See the design doc for the list.
    private static readonly Dictionary<string, UnitType> UnitNames = new()
    {
        ["graviton"] = UnitType.Graviton,
        ["repulsor"] = UnitType.Repulsor,
        ["gunturret"] = UnitType.GunTurret,
        ["turret"] = UnitType.GunTurret,
        ["launcher"] = UnitType.Launcher,
        ["missilelauncher"] = UnitType.Launcher,
        ["volcano"] = UnitType.Volcano,
        ["prison"] = UnitType.Prison,
        ["fuel"] = UnitType.FuelDump,
        ["fueldump"] = UnitType.FuelDump,
        ["flyingturret"] = UnitType.FlyingTurret,
        ["flyinglauncher"] = UnitType.FlyingLauncher,
        ["mothership"] = UnitType.MotherShip,
        ["tree"] = UnitType.Tree,
        ["building"] = UnitType.Building,
        ["decorativebuilding"] = UnitType.Building,
    };

    // Property names as the level designer may have typed them, mapped to the names the game uses.
    private static readonly Dictionary<string, string> PropertyNames = new()
    {
        ["strength"] = "Strength",
        ["triggerrange"] = "TriggerRange",
        ["aggression"] = "Aggression",
        ["agression"] = "Aggression",
        ["captives"] = "Captives",
        ["fuel"] = "Fuel",
    };

    public static LevelData ImportFile(string path)
    {
        string json = File.ReadAllText(path);
        return Import(json, Path.GetFileNameWithoutExtension(path));
    }

    public static LevelData Import(string json, string? fallbackName = null)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new LevelFormatException($"The level file is not valid JSON: {e.Message}");
        }

        using (doc)
        {
            return ImportMap(doc.RootElement, fallbackName);
        }
    }

    private static LevelData ImportMap(JsonElement root, string? fallbackName)
    {
        var warnings = new List<string>();

        if (GetBool(root, "infinite"))
        {
            throw new LevelFormatException("Infinite maps are not supported. Turn off 'infinite' in the map settings.");
        }

        int columns = RequiredInt(root, "width", "map");
        int rows = RequiredInt(root, "height", "map");
        int tileWidth = RequiredInt(root, "tilewidth", "map");
        int tileHeight = RequiredInt(root, "tileheight", "map");
        if (tileWidth != tileHeight)
        {
            throw new LevelFormatException($"Map cells must be square, but they are {tileWidth} by {tileHeight}.");
        }

        var mapProperties = ReadProperties(root);
        string name = mapProperties.TryGetValue("mapName", out var mapName) && mapName.ValueKind == JsonValueKind.String
            ? mapName.GetString() ?? "Level"
            : fallbackName ?? "Level";

        var tilesets = ReadTilesets(root);

        Terrain? terrain = null;
        Vec2? playerStart = null;
        var units = new List<UnitSpawn>();
        var background = new List<BackgroundTile>();

        if (root.TryGetProperty("layers", out var layers))
        {
            foreach (var layer in layers.EnumerateArray())
            {
                string type = GetString(layer, "type");
                string layerName = GetString(layer, "name");

                if (type == "tilelayer" && IsCollisionLayer(layer, layerName))
                {
                    terrain = ReadTerrain(layer, columns, rows, tileWidth);
                }
                else if (type == "objectgroup" && string.Equals(layerName, SpawnLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    ReadSpawns(layer, units, ref playerStart, warnings);
                }
                else if (type == "objectgroup")
                {
                    ReadBackground(layer, tilesets, background);
                }
            }
        }

        if (terrain is null)
        {
            throw new LevelFormatException(
                "The map has no collision layer. Add a tile layer named 'Collision' and fill the solid cells.");
        }
        if (playerStart is null)
        {
            throw new LevelFormatException(
                "The map has no player start. Add a point named 'Player Spawn' on the 'Spawn Points' layer.");
        }

        if (terrain.CircleHits(playerStart.Value, new GameConfig().ShipRadius))
        {
            warnings.Add("The player start is inside solid terrain.");
        }

        return new LevelData
        {
            Name = name,
            Terrain = terrain,
            PlayerStart = playerStart.Value,
            Tilesets = tilesets,
            Background = background,
            Units = units,
            Settings = ReadSettings(mapProperties),
            Warnings = warnings,
        };
    }

    private static List<TilesetInfo> ReadTilesets(JsonElement root)
    {
        var result = new List<TilesetInfo>();
        if (!root.TryGetProperty("tilesets", out var tilesets))
        {
            return result;
        }

        foreach (var ts in tilesets.EnumerateArray())
        {
            if (ts.TryGetProperty("source", out _))
            {
                throw new LevelFormatException(
                    "External tilesets are not supported. Export with the tileset embedded in the map file.");
            }

            result.Add(new TilesetInfo(
                Image: GetString(ts, "image"),
                FirstGid: RequiredInt(ts, "firstgid", "tileset"),
                TileWidth: RequiredInt(ts, "tilewidth", "tileset"),
                TileHeight: RequiredInt(ts, "tileheight", "tileset"),
                Columns: Math.Max(1, GetInt(ts, "columns", 1))));
        }

        result.Sort((a, b) => a.FirstGid.CompareTo(b.FirstGid));
        return result;
    }

    private static bool IsCollisionLayer(JsonElement layer, string layerName)
    {
        var props = ReadProperties(layer);
        if (props.TryGetValue("collisionLayer", out var flag) && flag.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        return string.Equals(layerName, "Collision", StringComparison.OrdinalIgnoreCase);
    }

    private static Terrain ReadTerrain(JsonElement layer, int columns, int rows, int cellSize)
    {
        if (layer.TryGetProperty("encoding", out _) || !layer.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new LevelFormatException("The collision layer must be stored as a plain list of numbers, not encoded or chunked.");
        }

        int layerColumns = GetInt(layer, "width", columns);
        int layerRows = GetInt(layer, "height", rows);
        if (data.GetArrayLength() != layerColumns * layerRows)
        {
            throw new LevelFormatException(
                $"The collision layer should hold {layerColumns * layerRows} cells but holds {data.GetArrayLength()}.");
        }

        var solid = new bool[layerColumns, layerRows];
        int i = 0;
        foreach (var cell in data.EnumerateArray())
        {
            // Any non-zero tile counts as solid. The tile number differs between exports.
            solid[i % layerColumns, i / layerColumns] = cell.GetUInt32() != 0;
            i++;
        }
        return new Terrain(solid, cellSize);
    }

    private static void ReadSpawns(JsonElement layer, List<UnitSpawn> units, ref Vec2? playerStart, List<string> warnings)
    {
        if (!layer.TryGetProperty("objects", out var objects))
        {
            return;
        }

        foreach (var obj in objects.EnumerateArray())
        {
            string pointName = GetString(obj, "name");
            var position = new Vec2(GetFloat(obj, "x"), GetFloat(obj, "y"));
            string key = NameKey(pointName);

            if (key == "player")
            {
                if (playerStart is not null)
                {
                    throw new LevelFormatException("The map has more than one 'Player Spawn'. Keep only one.");
                }
                playerStart = position;
                continue;
            }

            UnitType unitType;
            if (key == "flyer")
            {
                unitType = UnitType.FlyingTurret;
                warnings.Add($"Point '{pointName}' is treated as a Flying Turret. Rename it to 'Flying Turret Spawn' or 'Flying Launcher Spawn'.");
            }
            else if (!UnitNames.TryGetValue(key, out unitType))
            {
                throw new LevelFormatException(
                    $"Unknown spawn point name '{pointName}' at ({position.X:0}, {position.Y:0}). " +
                    "Names are '<Unit> Spawn', where the unit is one of: Player, Graviton, Repulsor, Gun Turret, Launcher, Volcano, " +
                    "Prison, Fuel, Flying Turret, Flying Launcher, Mother Ship, Tree, Building.");
            }

            var settings = new Dictionary<string, double>();
            foreach (var (propertyName, value) in ReadProperties(obj))
            {
                if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                {
                    continue; // layerId and other text properties are not game settings
                }

                string canonical = PropertyNames.TryGetValue(propertyName.ToLowerInvariant(), out var known) ? known : propertyName;
                settings[canonical] = value.ValueKind switch
                {
                    JsonValueKind.Number => value.GetDouble(),
                    JsonValueKind.True => 1d,
                    _ => 0d,
                };
            }

            units.Add(new UnitSpawn { Type = unitType, Name = pointName, Position = position, Settings = settings });
        }
    }

    private static void ReadBackground(JsonElement layer, List<TilesetInfo> tilesets, List<BackgroundTile> background)
    {
        if (layer.TryGetProperty("visible", out var layerVisible) && layerVisible.ValueKind == JsonValueKind.False)
        {
            return;
        }
        if (!layer.TryGetProperty("objects", out var objects))
        {
            return;
        }

        float layerX = GetFloat(layer, "x");
        float layerY = GetFloat(layer, "y");

        foreach (var obj in objects.EnumerateArray())
        {
            if (!obj.TryGetProperty("gid", out var gidElement) || GetBool(obj, "visible", true) == false)
            {
                continue;
            }

            uint raw = gidElement.GetUInt32();
            int gid = (int)(raw & GidMask);
            int tilesetIndex = FindTileset(tilesets, gid);
            if (tilesetIndex < 0)
            {
                continue;
            }

            float width = GetFloat(obj, "width");
            float height = GetFloat(obj, "height");

            // Tile objects are positioned by their bottom-left corner, so move up by the height.
            background.Add(new BackgroundTile(
                Tileset: tilesetIndex,
                TileIndex: gid - tilesets[tilesetIndex].FirstGid,
                X: layerX + GetFloat(obj, "x"),
                Y: layerY + GetFloat(obj, "y") - height,
                Width: width,
                Height: height,
                FlipHorizontal: (raw & FlipHorizontalBit) != 0,
                FlipVertical: (raw & FlipVerticalBit) != 0,
                FlipDiagonal: (raw & FlipDiagonalBit) != 0));
        }
    }

    private static int FindTileset(List<TilesetInfo> tilesets, int gid)
    {
        int found = -1;
        for (int i = 0; i < tilesets.Count; i++)
        {
            if (tilesets[i].FirstGid <= gid)
            {
                found = i;
            }
        }
        return found;
    }

    private static LevelSettings? ReadSettings(Dictionary<string, JsonElement> mapProperties)
    {
        bool hasGravity = mapProperties.TryGetValue("Gravity", out var gravity) && gravity.ValueKind == JsonValueKind.Number;
        bool hasThrust = mapProperties.TryGetValue("ThrustAccel", out var thrust) && thrust.ValueKind == JsonValueKind.Number;
        if (!hasGravity && !hasThrust)
        {
            return null;
        }

        var baseline = new GameConfig();
        return new LevelSettings(
            Gravity: hasGravity ? gravity.GetSingle() : baseline.Gravity,
            ThrustAccel: hasThrust ? thrust.GetSingle() : baseline.ThrustAccel);
    }

    /// <summary>"Flying Turret Spawn" becomes "flyingturret".</summary>
    private static string NameKey(string name)
    {
        string trimmed = name.Trim();
        const string suffix = "spawn";
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^suffix.Length];
        }
        return new string(trimmed.Where(char.IsLetter).ToArray()).ToLowerInvariant();
    }

    private static Dictionary<string, JsonElement> ReadProperties(JsonElement element)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in props.EnumerateArray())
            {
                if (prop.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && prop.TryGetProperty("value", out var v))
                {
                    result[n.GetString()!] = v;
                }
            }
        }
        return result;
    }

    private static string GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static float GetFloat(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : 0f;

    private static int GetInt(JsonElement e, string name, int fallback) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    private static bool GetBool(JsonElement e, string name, bool fallback = false) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.ValueKind == JsonValueKind.True
            : fallback;

    private static int RequiredInt(JsonElement e, string name, string where)
    {
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
        {
            return v.GetInt32();
        }
        throw new LevelFormatException($"The {where} is missing '{name}'.");
    }
}
