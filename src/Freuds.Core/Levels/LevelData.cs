using Freuds.Core.Model;

namespace Freuds.Core.Levels;

/// <summary>Everything the game needs to start a level, read from a TileMap export.</summary>
public sealed class LevelData
{
    public required string Name { get; init; }

    /// <summary>Terrain used for collisions, built from the Collision layer.</summary>
    public required Terrain Terrain { get; init; }

    /// <summary>Where the player's ship starts, in pixels.</summary>
    public required Vec2 PlayerStart { get; init; }

    public required IReadOnlyList<TilesetInfo> Tilesets { get; init; }

    /// <summary>Static art, in drawing order. It has no effect on collisions.</summary>
    public required IReadOnlyList<BackgroundTile> Background { get; init; }

    /// <summary>Enemy units, prisons, fuel dumps and so on, placed as spawn points.</summary>
    public required IReadOnlyList<UnitSpawn> Units { get; init; }

    /// <summary>Per-level physics from map properties. Null when the map does not set any.</summary>
    public LevelSettings? Settings { get; init; }

    /// <summary>Things the importer accepted but the level designer may want to fix.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>An image of tiles. <see cref="FirstGid"/> is the id TileMap gives to its first tile.</summary>
public sealed record TilesetInfo(string Image, int FirstGid, int TileWidth, int TileHeight, int Columns);

/// <summary>
/// One piece of background art. <see cref="TileIndex"/> counts from 0 along the rows of the tileset image.
/// X and Y are the top-left corner in pixels.
/// </summary>
public readonly record struct BackgroundTile(
    int Tileset,
    int TileIndex,
    float X,
    float Y,
    float Width,
    float Height,
    bool FlipHorizontal,
    bool FlipVertical,
    bool FlipDiagonal);

public enum UnitType
{
    Graviton,
    Repulsor,
    GunTurret,
    Launcher,
    Volcano,
    Prison,
    FuelDump,
    FlyingTurret,
    FlyingLauncher,
    MotherShip,
    Tree,
    Building,
}

/// <summary>A unit placed in the level, with the settings the level designer gave it.</summary>
public sealed class UnitSpawn
{
    public required UnitType Type { get; init; }

    /// <summary>The name of the point in TileMap, for error messages.</summary>
    public required string Name { get; init; }

    /// <summary>Position in pixels.</summary>
    public required Vec2 Position { get; init; }

    /// <summary>
    /// Numeric settings by canonical name: Strength, TriggerRange, Aggression, Captives, Fuel.
    /// The file's spelling "Agression" is stored as "Aggression".
    /// </summary>
    public required IReadOnlyDictionary<string, double> Settings { get; init; }

    public float Setting(string name, float fallback = 0f) =>
        Settings.TryGetValue(name, out var value) ? (float)value : fallback;
}

/// <summary>Thrown when a map file cannot be used. The message says what to fix in TileMap.</summary>
public sealed class LevelFormatException : Exception
{
    public LevelFormatException(string message) : base(message)
    {
    }
}
