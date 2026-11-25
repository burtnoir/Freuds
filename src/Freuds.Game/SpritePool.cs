using Freuds.Core;

namespace Freuds.Game;

public enum SpriteKind
{
    Ship,
    Bolt,
}

/// <summary>A drawable stand-in for one model object. Rebuilt from the model every tick.</summary>
public sealed class Sprite
{
    public SpriteKind Kind;
    public Vec2 Position;
    public float Angle;
    public ShipLook ShipLook;
    public bool Thrusting;
}

public enum ShipLook
{
    Flying,
    Landed,
    Crashed,
}

/// <summary>
/// Reuses sprite objects between ticks so rebuilding the view does not allocate. Sprites of one kind differ
/// only by position and angle, so any pooled sprite can stand in for any model object.
/// </summary>
public sealed class SpritePool
{
    private readonly List<Sprite> _items = new();
    private int _count;

    public int Count => _count;

    public Sprite this[int index] => _items[index];

    /// <summary>Forget the sprites from the last tick. Objects stay in the pool for reuse.</summary>
    public void Reset() => _count = 0;

    public Sprite Next()
    {
        if (_count == _items.Count)
        {
            _items.Add(new Sprite());
        }
        return _items[_count++];
    }
}
