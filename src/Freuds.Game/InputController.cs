using Freuds.Core.Events;
using Microsoft.Xna.Framework.Input;

namespace Freuds.Game;

/// <summary>
/// Turns keyboard state into events. It never touches the model directly: the model listens for
/// <see cref="PlayerInputEvent"/> through the event manager.
/// </summary>
public sealed class InputController
{
    private readonly EventManager _events;
    private bool _restartWasDown;

    public bool QuitRequested { get; private set; }

    public InputController(EventManager events)
    {
        _events = events;
    }

    /// <summary>Reads the keyboard, publishes the controls, and returns true if the player asked to restart.</summary>
    public bool Poll()
    {
        var keys = Keyboard.GetState();

        QuitRequested = keys.IsKeyDown(Keys.Escape);

        _events.Publish(new PlayerInputEvent(
            RotateLeft: keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A),
            RotateRight: keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D),
            Thrust: keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W),
            Fire: keys.IsKeyDown(Keys.Space)));

        bool restartDown = keys.IsKeyDown(Keys.R);
        bool restartPressed = restartDown && !_restartWasDown;
        _restartWasDown = restartDown;
        return restartPressed;
    }
}
