namespace Freuds.Core.Model;

/// <summary>
/// Tunable game constants. Distances are in pixels and times in seconds.
/// All values are placeholders to be adjusted by play testing, not figures from the old spec.
/// </summary>
public sealed class GameConfig
{
    /// <summary>Downward pull on the ship, roughly moon-like.</summary>
    public float Gravity { get; set; } = 120f;

    /// <summary>Acceleration from the main engine. Must exceed gravity or the ship cannot climb.</summary>
    public float ThrustAccel { get; set; } = 300f;

    /// <summary>Rotation speed in radians per second.</summary>
    public float RotationSpeed { get; set; } = 3.0f;

    public float MaxFuel { get; set; } = 100f;

    /// <summary>Fuel used per second of engine burn.</summary>
    public float FuelBurnRate { get; set; } = 8f;

    public float ShipRadius { get; set; } = 12f;

    public float BoltSpeed { get; set; } = 520f;
    public float BoltLifetime { get; set; } = 1.6f;
    public float BoltRadius { get; set; } = 2f;

    /// <summary>Game rule from the spec: at most this many energy bolts in flight.</summary>
    public int MaxBolts { get; set; } = 20;

    /// <summary>Minimum time between shots.</summary>
    public float FireInterval { get; set; } = 0.15f;

    /// <summary>Largest tilt, in radians, that still counts as vertical for landing.</summary>
    public float LandingMaxAngle { get; set; } = 0.12f;

    public float LandingMaxVerticalSpeed { get; set; } = 70f;
    public float LandingMaxHorizontalSpeed { get; set; } = 40f;

    /// <summary>How long, in seconds, the ship stays pinned to the top of the level before it can fall back.</summary>
    public float CeilingStickTime { get; set; } = 1f;

    /// <summary>The small upward speed the ship keeps while pinned to the top of the level.</summary>
    public float CeilingResidualSpeed { get; set; } = 8f;

    /// <summary>
    /// How far above the highest terrain point the ship's bottom must be for the mother ship to appear.
    /// </summary>
    public float MotherShipMargin { get; set; } = 64f;
}
