using Freuds.Core;
using Freuds.Core.Events;
using Freuds.Core.Levels;
using Freuds.Core.Model;

// Plain console test runner so the tests need no NuGet packages. Run with: dotnet run --project tests/Freuds.Tests
// Exit code is the number of failed checks.

int passed = 0;
var failures = new List<string>();

void Check(string name, bool ok, string detail = "")
{
    if (ok)
    {
        passed++;
        Console.WriteLine($"  ok   {name}");
    }
    else
    {
        failures.Add(name);
        Console.WriteLine($"  FAIL {name} {detail}");
    }
}

bool Near(float a, float b, float tol = 0.01f) => MathF.Abs(a - b) <= tol;

const float Dt = 1f / 60f;

// 20 by 20 cells of 32 px. Rows 18 and 19 are solid, so the flat ground top is y = 576.
const float GroundTop = 18 * 32f;

Terrain FlatGround()
{
    var rows = new List<string>();
    for (int r = 0; r < 20; r++)
    {
        rows.Add(r >= 18 ? new string('#', 20) : new string('.', 20));
    }
    return Terrain.FromRows(rows, 32f);
}

(World world, EventManager events, GameConfig cfg) Make(Terrain terrain, Vec2 start)
{
    var events = new EventManager();
    var cfg = new GameConfig();
    var world = new World(terrain, cfg, events, start);
    return (world, events, cfg);
}

void Step(EventManager events, int ticks, PlayerInputEvent? input = null)
{
    for (int i = 0; i < ticks; i++)
    {
        if (input.HasValue)
        {
            events.Publish(input.Value);
        }
        events.Publish(new TickEvent(Dt));
    }
}

var none = new PlayerInputEvent(false, false, false, false);
var thrust = new PlayerInputEvent(false, false, true, false);
var fire = new PlayerInputEvent(false, false, false, true);

Console.WriteLine("EventManager");
{
    var events = new EventManager();
    int count = 0;
    Action<TickEvent> handler = _ => count++;
    events.Subscribe(handler);
    events.Publish(new TickEvent(Dt));
    events.Publish(new PlayerInputEvent(false, false, false, false));
    Check("delivers only events of the subscribed type", count == 1);
    events.Unsubscribe(handler);
    events.Publish(new TickEvent(Dt));
    Check("unsubscribe stops delivery", count == 1);

    int late = 0;
    events.Subscribe<TickEvent>(_ => events.Subscribe<TickEvent>(__ => late++));
    events.Publish(new TickEvent(Dt));
    Check("subscribing inside a handler does not break delivery", late == 0);
}

Console.WriteLine("Terrain");
{
    var t = FlatGround();
    Check("cells above the ground are empty", !t.IsSolidAt(100, 100));
    Check("ground cells are solid", t.IsSolidAt(100, 600));
    Check("below the last row counts as solid", t.IsSolidAt(100, 5000));
    Check("above the first row is open sky", !t.IsSolidAt(100, -500));
    Check("highest terrain point is the top of the first solid row", Near(t.HighestTerrainY, GroundTop));
    Check("wrap keeps x inside the world", Near(t.WrapX(-10f), t.WidthPx - 10f) && Near(t.WrapX(t.WidthPx + 5f), 5f));
    Check("wrap delta takes the short way round", Near(t.WrapDelta(t.WidthPx - 10f, 10f), 20f));

    var seam = Terrain.FromRows(new[] { "#...", "#..." }, 32f);
    Check("solid cells wrap across the seam", seam.IsSolidAt(seam.WidthPx + 5f, 10f) && !seam.IsSolidAt(-5f, 10f) && seam.IsSolidAt(0f, 10f));
    Check("circle near the seam hits the wrapped cell", seam.CircleHits(new Vec2(seam.WidthPx - 2f, 10f), 6f));
    Check("circle touching only a corner outside its radius misses", !t.CircleHits(new Vec2(100, GroundTop - 13f), 12f));
    Check("circle overlapping the ground hits", t.CircleHits(new Vec2(100, GroundTop - 11f), 12f));
}

Console.WriteLine("Ship flight");
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 100));
    Step(events, 60, none);
    Check("gravity gives about one second of acceleration", Near(world.Player.Velocity.Y, cfg.Gravity, 1f), $"vy={world.Player.Velocity.Y}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 400));
    Step(events, 30, thrust);
    Check("upright thrust beats gravity and climbs", world.Player.Velocity.Y < 0f, $"vy={world.Player.Velocity.Y}");
    Check("burning the engine uses fuel", Near(world.Player.Fuel, cfg.MaxFuel - cfg.FuelBurnRate * 0.5f, 0.1f), $"fuel={world.Player.Fuel}");
}
{
    var (w2, e2, cfg2) = Make(FlatGround(), new Vec2(300, 100));
    cfg2.FuelBurnRate = 1000f;
    Step(e2, 8, thrust);
    float before = w2.Player.Velocity.Y;
    Check("fuel runs out", w2.Player.Fuel == 0f);
    Step(e2, 10, thrust);
    Check("no thrust once empty, gravity wins again", w2.Player.Velocity.Y > before);
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 100));
    Step(events, 30, new PlayerInputEvent(false, true, false, false));
    Check("rotate right turns clockwise", Near(world.Player.Angle, cfg.RotationSpeed * 0.5f, 0.05f), $"a={world.Player.Angle}");
    Step(events, 30, new PlayerInputEvent(true, false, false, false));
    Check("rotate left turns back", Near(world.Player.Angle, 0f, 0.05f), $"a={world.Player.Angle}");
}
{
    var (world, events, _) = Make(FlatGround(), new Vec2(300, 100));
    world.Player.Position = new Vec2(world.Terrain.WidthPx - 1f, 100f);
    world.Player.Velocity = new Vec2(120f, 0f);
    Step(events, 3, none);
    Check("ship wraps off the right edge onto the left", world.Player.Position.X >= 0f && world.Player.Position.X < 20f, $"x={world.Player.Position.X}");
}

Console.WriteLine("Landing and crashing");
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, GroundTop - cfg0().ShipRadius - 8f));
    int landed = 0;
    events.Subscribe<ShipLandedEvent>(_ => landed++);
    Step(events, 30, none);
    Check("slow upright touchdown on flat ground lands", world.Player.State == ShipState.Landed, $"state={world.Player.State}");
    Check("landing event is published once", landed == 1);
    Check("landed ship rests on the ground without sinking", !world.Terrain.CircleHits(world.Player.Position, cfg.ShipRadius));
    Step(events, 30, thrust);
    Check("thrust takes off from a landing", world.Player.State == ShipState.Flying && world.Player.Position.Y < GroundTop - cfg.ShipRadius - 1f);
}
{
    var (world, events, _) = Make(FlatGround(), new Vec2(300, 100));
    int crashed = 0;
    events.Subscribe<ShipCrashedEvent>(_ => crashed++);
    Step(events, 600, none);
    Check("falling fast into the ground crashes", world.Player.State == ShipState.Crashed);
    Check("crash event is published once", crashed == 1);
    var before = world.Player.Position;
    Step(events, 30, thrust);
    Check("a crashed ship stays put", world.Player.Position.X == before.X && world.Player.Position.Y == before.Y);
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, GroundTop - cfg0().ShipRadius - 8f));
    world.Player.Angle = 0.5f;
    Step(events, 30, none);
    Check("tilted touchdown crashes", world.Player.State == ShipState.Crashed);
}
{
    var (world, events, _) = Make(FlatGround(), new Vec2(300, GroundTop - cfg0().ShipRadius - 8f));
    world.Player.Velocity = new Vec2(120f, 0f);
    Step(events, 30, none);
    Check("sliding sideways onto the ground crashes", world.Player.State == ShipState.Crashed);
}
{
    // Ground steps up at column 10, so a ship straddling the step is not on a flat area.
    var rows = new List<string>();
    for (int r = 0; r < 20; r++)
    {
        string row = r >= 18 ? new string('#', 20) : (r == 17 ? new string('.', 10) + new string('#', 10) : new string('.', 20));
        rows.Add(row);
    }
    var step = Terrain.FromRows(rows, 32f);
    var (world, events, _) = Make(step, new Vec2(320f, 17 * 32f - cfg0().ShipRadius - 8f));
    Step(events, 30, none);
    Check("landing on a step crashes", world.Player.State == ShipState.Crashed, $"state={world.Player.State}");
}

Console.WriteLine("Cannon");
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    Step(events, 30, fire);
    Check("holding fire launches bolts at the fire interval", world.Bolts.Count >= 2 && world.Bolts.Count <= cfg.MaxBolts, $"bolts={world.Bolts.Count}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    cfg.FireInterval = 0f;
    Step(events, 25, fire);
    Check("the 20 bolt cap is reached and held", world.Bolts.Count == 20, $"bolts={world.Bolts.Count}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    world.Player.Angle = MathF.PI / 2f; // facing right
    world.Player.Velocity = new Vec2(100f, -50f); // travelling right and up
    Step(events, 1, fire);
    var shipVel = world.Player.Velocity; // the ship updates before it fires, so this is the speed at launch
    var bolt = world.Bolts[0];
    Check("bolt velocity is the ship's velocity plus the launch velocity",
        Near(bolt.Velocity.X, shipVel.X + cfg.BoltSpeed, 0.5f) && Near(bolt.Velocity.Y, shipVel.Y, 0.5f), $"bolt={bolt.Velocity} ship={shipVel}");
    var launched = bolt.Velocity;
    Step(events, 5, none);
    Check("a bolt keeps its launch velocity (gravity does not act on it)",
        Near(world.Bolts[0].Velocity.X, launched.X, 0.01f) && Near(world.Bolts[0].Velocity.Y, launched.Y, 0.01f));
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    world.Player.Velocity = new Vec2(0f, 0f);
    Step(events, 1, fire); // facing up
    var shipVel = world.Player.Velocity;
    var bolt = world.Bolts[0];
    Check("launch direction is the way the ship points",
        Near(bolt.Velocity.X, shipVel.X, 0.5f) && Near(bolt.Velocity.Y, shipVel.Y - cfg.BoltSpeed, 0.5f), $"bolt={bolt.Velocity}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    Step(events, 1, fire);
    Step(events, (int)(cfg.BoltLifetime / Dt) + 5, none);
    Check("bolts expire after their lifetime or on terrain", world.Bolts.Count == 0, $"bolts={world.Bolts.Count}");
}
{
    var (world, events, _) = Make(FlatGround(), new Vec2(300, 540));
    world.Player.Angle = MathF.PI; // facing down at the ground
    Step(events, 1, fire);
    Step(events, 30, none);
    Check("bolts are destroyed by terrain", world.Bolts.Count == 0);
}

Console.WriteLine("Top of the level");
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 60));
    world.Player.Velocity = new Vec2(0f, -400f);
    float highest = float.MaxValue;
    void Fly(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step(events, 1, none);
            highest = MathF.Min(highest, world.Player.Position.Y - cfg.ShipRadius);
        }
    }

    Fly(15);
    Check("the ship reaches the top and stays inside the level", Near(world.Player.Position.Y, cfg.ShipRadius, 0.01f), $"y={world.Player.Position.Y}");
    Check("upward speed drops to a small value at the top", MathF.Abs(world.Player.Velocity.Y) <= 10f, $"vy={world.Player.Velocity.Y}");
    Fly(25);
    Check("it stays stuck to the top for a while", world.Player.Position.Y <= cfg.ShipRadius + 1f, $"y={world.Player.Position.Y}");
    Fly(80);
    Check("after about a second it drops back", world.Player.Position.Y > cfg.ShipRadius + 5f && world.Player.Velocity.Y > 0f, $"y={world.Player.Position.Y}");
    Check("the ship never goes above the top", highest >= -0.001f, $"highest={highest}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 60));
    world.Player.Velocity = new Vec2(0f, -100f);
    float highest = float.MaxValue;
    for (int i = 0; i < 200; i++)
    {
        Step(events, 1, thrust);
        highest = MathF.Min(highest, world.Player.Position.Y - cfg.ShipRadius);
    }
    Check("holding the engine on keeps the ship pressed against the top, not above it",
        highest >= -0.001f && world.Player.Position.Y <= cfg.ShipRadius + 1f, $"y={world.Player.Position.Y}");
}
{
    var (world, events, cfg) = Make(FlatGround(), new Vec2(300, 300));
    Step(events, 10, none);
    Check("away from the top nothing changes", world.Player.Position.Y > cfg.ShipRadius + 100f);
}

Console.WriteLine("World");
{
    var terrain = DemoLevel.Create();
    var (world, events, cfg) = Make(terrain, DemoLevel.PlayerStart);
    Check("demo level is 96 by 32 cells", terrain.Columns == 96 && terrain.Rows == 32);
    Check("demo level highest terrain is row 6", Near(terrain.HighestTerrainY, 6 * 32f));
    Check("demo level joins at the seam", terrain.IsSolidCell(0, 25) == terrain.IsSolidCell(95, 25));
    Check("start position is open air", !terrain.CircleHits(DemoLevel.PlayerStart, cfg.ShipRadius));
    Check("mother ship height is not reached on the ground", !world.AtMotherShipHeight);
    world.Player.Position = new Vec2(300f, terrain.HighestTerrainY - cfg.MotherShipMargin - cfg.ShipRadius - 5f);
    Check("flying above the highest terrain reaches mother ship height", world.AtMotherShipHeight);

    world.Player.Position = new Vec2(300f, 100f);
    world.Player.Velocity = new Vec2(0f, 300f);
    Step(events, 600, none);
    Check("ship crashes eventually", world.Player.State == ShipState.Crashed);
    world.Restart();
    Check("restart resets state, fuel and bolts", world.Player.State == ShipState.Flying && world.Player.Fuel == cfg.MaxFuel && world.Bolts.Count == 0
        && Near(world.Player.Position.X, DemoLevel.PlayerStart.X) && Near(world.Player.Position.Y, DemoLevel.PlayerStart.Y));
}

Console.WriteLine("Level settings");
{
    var baseline = new GameConfig();
    var settings = DemoLevel.Settings;
    Check("level 1 has half the default gravity", Near(settings.Gravity, baseline.Gravity * 0.5f));
    Check("level 1 has 20 percent more thrust", Near(settings.ThrustAccel, baseline.ThrustAccel * 1.2f));

    var events = new EventManager();
    var cfg = new GameConfig();
    settings.ApplyTo(cfg);
    Check("applying settings changes gravity and thrust only",
        Near(cfg.Gravity, 60f) && Near(cfg.ThrustAccel, 360f) && Near(cfg.FuelBurnRate, baseline.FuelBurnRate) && Near(cfg.RotationSpeed, baseline.RotationSpeed));

    var world = new World(DemoLevel.Create(), cfg, events, DemoLevel.PlayerStart);
    Step(events, 60, none);
    Check("ship in level 1 gains half the speed of the default after one second",
        Near(world.Player.Velocity.Y, 60f, 1f), $"vy={world.Player.Velocity.Y}");

    var other = new World(DemoLevel.Create(), new GameConfig { Gravity = 200f }, events, DemoLevel.PlayerStart);
    Check("another level can use a different gravity", Near(other.Config.Gravity, 200f));
}

Console.WriteLine("TileMap importer");
{
    string? dir = AppContext.BaseDirectory;
    string? file = null;
    while (dir is not null && file is null)
    {
        string candidate = Path.Combine(dir, "src", "Freuds.Game", "Levels", "rescue_example.json");
        if (File.Exists(candidate)) file = candidate;
        dir = Path.GetDirectoryName(dir);
    }
    Check("example map file is found", file is not null);
    if (file is not null)
    {
        var level = TileMapImporter.ImportFile(file);
        Check("level name comes from the map properties", level.Name == "Rescue_Example", level.Name);
        Check("terrain is 96 by 32 cells of 32 px", level.Terrain.Columns == 96 && level.Terrain.Rows == 32 && level.Terrain.CellSize == 32, $"{level.Terrain.Columns}x{level.Terrain.Rows}x{level.Terrain.CellSize}");
        Check("some terrain is solid", level.Terrain.HighestTerrainY < level.Terrain.HeightPx);
        Check("player start is found and clear of terrain", !level.Terrain.CircleHits(level.PlayerStart, new GameConfig().ShipRadius), $"{level.PlayerStart.X},{level.PlayerStart.Y}");
        var types = level.Units.Select(u => u.Type).ToHashSet();
        Check("units are read from spawn points",
            level.Units.Count == 5 && types.Contains(UnitType.Graviton) && types.Contains(UnitType.Launcher) && types.Contains(UnitType.Prison) && types.Contains(UnitType.FuelDump) && types.Contains(UnitType.FlyingTurret),
            string.Join(",", types));
        Check("old 'Flyer Spawn' name gives a warning", level.Warnings.Any(w => w.Contains("Flyer")));
        Check("art tiles are imported", level.Background.Count == 1910, $"{level.Background.Count}");
        Check("first tileset id is applied", level.Tilesets.Count == 1 && level.Tilesets[0].Columns == 84 && level.Background.All(t => t.TileIndex >= 0 && t.TileIndex < 84 * 84), $"{level.Tilesets.Count}");
        Check("flip flags are decoded", level.Background.Count(t => t.FlipHorizontal) == 76 && level.Background.Count(t => t.FlipVertical) == 18 && level.Background.All(t => !t.FlipDiagonal),
            $"h={level.Background.Count(t => t.FlipHorizontal)} v={level.Background.Count(t => t.FlipVertical)}");
        Check("art is placed inside the map", level.Background.All(t => t.X >= 0 && t.Y >= 0 && t.X + t.Width <= level.Terrain.WidthPx + 0.5f && t.Y + t.Height <= level.Terrain.HeightPx + 0.5f));
        Check("map with no Gravity property leaves settings unset", level.Settings is null);
    }

    string Minimal(string spawns, string extra = "", string collision = "[0,0,0,0]") =>
        "{\"width\":2,\"height\":2,\"tilewidth\":16,\"tileheight\":16,\"tilesets\":[]," + extra +
        "\"layers\":[{\"type\":\"tilelayer\",\"name\":\"Collision\",\"width\":2,\"height\":2,\"data\":" + collision + "}," +
        "{\"type\":\"objectgroup\",\"name\":\"Spawn Points\",\"objects\":[" + spawns + "]}]}";
    const string Player = "{\"name\":\"Player Spawn\",\"x\":8,\"y\":8}";

    var small = TileMapImporter.Import(Minimal(Player + ",{\"name\":\"Gun Turret Spawn\",\"x\":1,\"y\":2,\"properties\":[{\"name\":\"Agression\",\"type\":\"int\",\"value\":3}]}",
        "\"properties\":[{\"name\":\"Gravity\",\"type\":\"float\",\"value\":50}],"), "x");
    Check("minimal map imports", small.Units.Count == 1 && small.Units[0].Type == UnitType.GunTurret);
    Check("'Agression' is stored as Aggression", Near(small.Units[0].Setting("Aggression"), 3f));
    Check("map Gravity property sets level settings", small.Settings is not null && Near(small.Settings.Gravity, 50f) && Near(small.Settings.ThrustAccel, new GameConfig().ThrustAccel));

    static bool Throws(Action a) { try { a(); return false; } catch (LevelFormatException) { return true; } }
    Check("missing player start is an error", Throws(() => TileMapImporter.Import(Minimal(""))));
    Check("unknown spawn name is an error", Throws(() => TileMapImporter.Import(Minimal(Player + ",{\"name\":\"Banana Spawn\",\"x\":1,\"y\":1}"))));
    Check("two player starts is an error", Throws(() => TileMapImporter.Import(Minimal(Player + "," + Player))));
    Check("bad json is an error", Throws(() => TileMapImporter.Import("{nope")));
    Check("wrong collision size is an error", Throws(() => TileMapImporter.Import(Minimal(Player, collision: "[0,0,0]"))));
}

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failures.Count} failed");
foreach (var f in failures)
{
    Console.WriteLine($"  failed: {f}");
}
return failures.Count;

static GameConfig cfg0() => new();
