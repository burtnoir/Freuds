using Freuds.Core;
using Freuds.Core.Events;
using Freuds.Core.Levels;
using Freuds.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Freuds.Game;

/// <summary>Milestone 1: ship flight over a hard-coded level, drawn with placeholder shapes.</summary>
public sealed class FreudsGame : Microsoft.Xna.Framework.Game
{
    private const int ViewWidth = 1280;
    private const int ViewHeight = 720;

    private readonly GraphicsDeviceManager _graphics;
    private readonly EventManager _events = new();

    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    private World _world = null!;
    private InputController _input = null!;
    private GameView _view = null!;

    public FreudsGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ViewWidth,
            PreferredBackBufferHeight = ViewHeight,
        };
        IsMouseVisible = true;

        // Fixed 60 Hz update so physics behaves the same on every machine.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Order matters: the model subscribes to the tick event before the view does.
        var config = new GameConfig();
        DemoLevel.Settings.ApplyTo(config);
        _world = new World(DemoLevel.Create(), config, _events, DemoLevel.PlayerStart);
        _input = new InputController(_events);
        _view = new GameView(_events, _world, _pixel, ViewWidth, ViewHeight);
    }

    protected override void Update(GameTime gameTime)
    {
        bool restart = _input.Poll();
        if (_input.QuitRequested)
        {
            Exit();
            return;
        }

        if (restart)
        {
            _world.Restart();
        }

        _events.Publish(new TickEvent((float)TargetElapsedTime.TotalSeconds));

        var ship = _world.Player;
        Window.Title = $"Freuds - {ship.State}  fuel {ship.Fuel:0}  |  arrows or WASD fly, space fires, R restarts, Esc quits";

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 10, 24));
        _batch.Begin();
        _view.Draw(_batch);
        _batch.End();

        base.Draw(gameTime);
    }
}
