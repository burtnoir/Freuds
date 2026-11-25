using Freuds.Core.Events;

namespace Freuds.Core.Model;

public enum ShipState
{
    Flying,
    Landed,
    Crashed,
}

/// <summary>The player's ship: Asteroids / Thrust-style flight under gravity, with a cannon.</summary>
public sealed class Ship : MobileElement
{
    private float _fireCooldown;
    private bool _touchingCeiling;
    private float _ceilingTimeLeft;

    public ShipState State { get; private set; } = ShipState.Flying;
    public float Fuel { get; private set; }
    public bool Thrusting { get; private set; }

    public Ship(Vec2 position, GameConfig config)
    {
        Radius = config.ShipRadius;
        Position = position;
        Fuel = config.MaxFuel;
    }

    public void ResetTo(Vec2 position, GameConfig config)
    {
        Position = position;
        Velocity = Vec2.Zero;
        Angle = 0f;
        Fuel = config.MaxFuel;
        State = ShipState.Flying;
        Thrusting = false;
        _fireCooldown = 0f;
        _touchingCeiling = false;
        _ceilingTimeLeft = 0f;
    }

    public override void Update(float dt, World world)
    {
        if (State == ShipState.Crashed)
        {
            Thrusting = false;
            return;
        }

        var cfg = world.Config;
        var input = world.Input;

        Thrusting = input.Thrust && Fuel > 0f;
        if (Thrusting)
        {
            Fuel = MathF.Max(0f, Fuel - cfg.FuelBurnRate * dt);
            if (State == ShipState.Landed)
            {
                State = ShipState.Flying;
            }
        }

        if (State == ShipState.Flying)
        {
            float turn = (input.RotateRight ? 1f : 0f) - (input.RotateLeft ? 1f : 0f);
            Angle = NormalizeAngle(Angle + turn * cfg.RotationSpeed * dt);

            var accel = new Vec2(0f, cfg.Gravity);
            if (Thrusting)
            {
                accel += Vec2.FromAngle(Angle) * cfg.ThrustAccel;
            }

            Velocity += accel * dt;
            var next = Position + Velocity * dt;
            Position = new Vec2(world.Terrain.WrapX(next.X), next.Y);

            ApplyCeiling(cfg, dt);

            if (world.Terrain.CircleHits(Position, Radius))
            {
                ResolveContact(world);
            }
        }

        _fireCooldown = MathF.Max(0f, _fireCooldown - dt);
        if (State != ShipState.Crashed && input.Fire && _fireCooldown <= 0f && world.TryFireBolt(this))
        {
            _fireCooldown = cfg.FireInterval;
        }
    }

    /// <summary>
    /// The ship cannot leave the top of the level. On reaching it the ship is pinned there with only a small
    /// upward speed for <see cref="GameConfig.CeilingStickTime"/> seconds. After that, gravity can pull it back
    /// down, unless the player keeps the engine on, in which case it simply presses against the top.
    /// </summary>
    private void ApplyCeiling(GameConfig cfg, float dt)
    {
        float limit = Radius;

        if (Position.Y < limit)
        {
            Position = new Vec2(Position.X, limit);

            if (!_touchingCeiling)
            {
                _touchingCeiling = true;
                _ceilingTimeLeft = cfg.CeilingStickTime;
            }

            if (_ceilingTimeLeft > 0f)
            {
                _ceilingTimeLeft -= dt;
                Velocity = new Vec2(Velocity.X, -cfg.CeilingResidualSpeed);
            }
            else if (Velocity.Y < 0f)
            {
                Velocity = new Vec2(Velocity.X, 0f);
            }
        }
        else if (Position.Y > limit + 1f)
        {
            _touchingCeiling = false;
        }
    }

    private void ResolveContact(World world)
    {
        var cfg = world.Config;
        bool upright = MathF.Abs(Angle) <= cfg.LandingMaxAngle;
        bool slowEnough = Velocity.Y >= 0f
            && Velocity.Y <= cfg.LandingMaxVerticalSpeed
            && MathF.Abs(Velocity.X) <= cfg.LandingMaxHorizontalSpeed;

        if (upright && slowEnough && world.Terrain.IsFlatLandingSpot(Position, Radius) && TrySettle(world))
        {
            Velocity = Vec2.Zero;
            Angle = 0f;
            State = ShipState.Landed;
            world.Events.Publish(new ShipLandedEvent());
            return;
        }

        Velocity = Vec2.Zero;
        State = ShipState.Crashed;
        world.Events.Publish(new ShipCrashedEvent());
    }

    /// <summary>Lift the ship out of the ground a pixel at a time. Fails if it is wedged against a wall.</summary>
    private bool TrySettle(World world)
    {
        var pos = Position;
        for (int i = 0; i < 16; i++)
        {
            if (!world.Terrain.CircleHits(pos, Radius))
            {
                Position = pos;
                return true;
            }
            pos = new Vec2(pos.X, pos.Y - 1f);
        }
        return false;
    }

    private static float NormalizeAngle(float a)
    {
        while (a > MathF.PI) a -= 2f * MathF.PI;
        while (a < -MathF.PI) a += 2f * MathF.PI;
        return a;
    }
}
