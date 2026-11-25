namespace Freuds.Core.Model;

/// <summary>An energy bolt from the player's cannon. Flies straight and ignores gravity.</summary>
public sealed class PlasmaBolt : MobileElement
{
    private float _age;

    public bool Expired { get; private set; }

    public PlasmaBolt(Vec2 position, Vec2 velocity, float radius)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
    }

    public override void Update(float dt, World world)
    {
        if (Expired)
        {
            return;
        }

        _age += dt;
        var next = Position + Velocity * dt;
        Position = new Vec2(world.Terrain.WrapX(next.X), next.Y);

        if (_age >= world.Config.BoltLifetime || world.Terrain.IsSolidAt(Position.X, Position.Y))
        {
            Expired = true;
        }
    }
}
