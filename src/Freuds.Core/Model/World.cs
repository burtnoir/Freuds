using Freuds.Core.Events;

namespace Freuds.Core.Model;

/// <summary>
/// The game model. It reacts to tick and input events and never talks to the view.
/// The view reads <see cref="Player"/> and <see cref="Bolts"/> each tick to rebuild its sprites.
/// </summary>
public sealed class World
{
    private readonly List<PlasmaBolt> _bolts = new();
    private readonly Vec2 _playerStart;

    public Terrain Terrain { get; }
    public GameConfig Config { get; }
    public EventManager Events { get; }
    public Ship Player { get; }
    public IReadOnlyList<PlasmaBolt> Bolts => _bolts;

    /// <summary>The most recent controls published by the input controller.</summary>
    public PlayerInputEvent Input { get; private set; }

    public World(Terrain terrain, GameConfig config, EventManager events, Vec2 playerStart)
    {
        Terrain = terrain;
        Config = config;
        Events = events;
        _playerStart = playerStart;
        Player = new Ship(playerStart, config);

        events.Subscribe<PlayerInputEvent>(e => Input = e);
        events.Subscribe<TickEvent>(e => Update(e.Dt));
    }

    /// <summary>
    /// True once the ship's bottom is well above the highest terrain point. This is where the mother ship
    /// will appear and vertical scrolling stops.
    /// </summary>
    public bool AtMotherShipHeight =>
        Player.Position.Y + Player.Radius < Terrain.HighestTerrainY - Config.MotherShipMargin;

    public void Update(float dt)
    {
        Player.Update(dt, this);

        foreach (var bolt in _bolts)
        {
            bolt.Update(dt, this);
        }
        _bolts.RemoveAll(b => b.Expired);
    }

    /// <summary>
    /// Fires one bolt from the ship's nose if fewer than the maximum are in flight. The bolt launches in the
    /// direction the ship points, and its velocity is the ship's velocity plus the launch velocity.
    /// </summary>
    public bool TryFireBolt(Ship ship)
    {
        if (_bolts.Count >= Config.MaxBolts)
        {
            return false;
        }

        var dir = Vec2.FromAngle(ship.Angle);
        var start = ship.Position + dir * (ship.Radius + Config.BoltRadius + 2f);
        var velocity = ship.Velocity + dir * Config.BoltSpeed;
        _bolts.Add(new PlasmaBolt(new Vec2(Terrain.WrapX(start.X), start.Y), velocity, Config.BoltRadius)
        {
            Angle = ship.Angle,
        });
        return true;
    }

    public void Restart()
    {
        _bolts.Clear();
        Player.ResetTo(_playerStart, Config);
    }
}
