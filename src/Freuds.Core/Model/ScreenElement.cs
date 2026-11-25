namespace Freuds.Core.Model;

/// <summary>Anything that exists in the game world. Knows nothing about how it is drawn.</summary>
public abstract class ScreenElement
{
    public Vec2 Position { get; set; }

    public float Radius { get; protected set; }

    /// <summary>Null means indestructible.</summary>
    public int? HitPoints { get; set; }

    public abstract void Update(float dt, World world);
}

/// <summary>A screen element that moves. These are the objects the view rebuilds sprites for every tick.</summary>
public abstract class MobileElement : ScreenElement
{
    public Vec2 Velocity { get; set; }

    /// <summary>Facing in radians. 0 points up and positive turns clockwise.</summary>
    public float Angle { get; set; }
}
