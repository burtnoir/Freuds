using Freuds.Core.Model;

namespace Freuds.Core.Levels;

/// <summary>
/// Physics settings that belong to one level. Each level can set its own gravity and engine power.
/// Later these will be read from map properties in TileMap.
/// </summary>
public sealed record LevelSettings(float Gravity, float ThrustAccel)
{
    /// <summary>Copy these settings onto a game config. Other config values are left alone.</summary>
    public void ApplyTo(GameConfig config)
    {
        config.Gravity = Gravity;
        config.ThrustAccel = ThrustAccel;
    }
}
