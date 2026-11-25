namespace Freuds.Core.Events;

/// <summary>Published once per fixed update by the game loop. The model and the view both react to it.</summary>
public readonly record struct TickEvent(float Dt);

/// <summary>The player's current controls, published every frame by the input controller.</summary>
public readonly record struct PlayerInputEvent(bool RotateLeft, bool RotateRight, bool Thrust, bool Fire);

/// <summary>Published by the model when the ship touches terrain badly. Useful for sound later.</summary>
public readonly record struct ShipCrashedEvent();

/// <summary>Published by the model when the ship settles on a flat landing area.</summary>
public readonly record struct ShipLandedEvent();
