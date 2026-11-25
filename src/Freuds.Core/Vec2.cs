namespace Freuds.Core;

/// <summary>Minimal 2D vector so Core has no dependency on MonoGame. Y points down, like the screen.</summary>
public readonly struct Vec2
{
    public readonly float X;
    public readonly float Y;

    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0f, 0f);

    public float Length => MathF.Sqrt(X * X + Y * Y);

    /// <summary>Unit vector for a ship angle. Angle 0 points up, positive angles turn clockwise.</summary>
    public static Vec2 FromAngle(float angle) => new(MathF.Sin(angle), -MathF.Cos(angle));

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(float s, Vec2 a) => new(a.X * s, a.Y * s);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}
