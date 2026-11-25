using System.IO;
using Freuds.Core;
using Freuds.Core.Events;
using Freuds.Core.Levels;
using Freuds.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Freuds.Game;

/// <summary>Flies the ship over a TileMap level (Levels/rescue_example.json), or the demo level if none loads.</summary>
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

        var config = new GameConfig();
        var level = TryLoadLevel();
        Texture2D?[]? tilesets = null;
        Terrain terrain;
        Vec2 start;

        if (level is not null)
        {
            // Map properties may set gravity and thrust. Otherwise level 1's tuned values are used.
            (level.Settings ?? DemoLevel.Settings).ApplyTo(config);
            terrain = level.Terrain;
            start = level.PlayerStart;
            tilesets = LoadTilesets(level);
        }
        else
        {
            DemoLevel.Settings.ApplyTo(config);
            terrain = DemoLevel.Create();
            start = DemoLevel.PlayerStart;
        }

        // Order matters: the model subscribes to the tick event before the view does.
        _world = new World(terrain, config, _events, start);
        _input = new InputController(_events);
        _view = new GameView(_events, _world, _pixel, ViewWidth, ViewHeight, level, tilesets);
    }

    private string _levelDir = "";

    /// <summary>Loads the level named on the command line, or Levels/rescue_example.json beside the game.</summary>
    private LevelData? TryLoadLevel()
    {
        var args = Environment.GetCommandLineArgs();
        string path = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "Levels", "rescue_example.json");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Level file not found: {path}. Using the demo level.");
            return null;
        }

        try
        {
            var level = TileMapImporter.ImportFile(path);
            _levelDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            foreach (var warning in level.Warnings)
            {
                Console.Error.WriteLine($"Level warning: {warning}");
            }
            return level;
        }
        catch (LevelFormatException e)
        {
            Console.Error.WriteLine($"Could not use {path}: {e.Message} Using the demo level.");
            return null;
        }
    }

    private Texture2D?[] LoadTilesets(LevelData level)
    {
        var result = new Texture2D?[level.Tilesets.Count];
        for (int i = 0; i < result.Length; i++)
        {
            string file = Path.Combine(_levelDir, level.Tilesets[i].Image);
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"Tileset image not found: {file}. Drawing placeholder terrain.");
                continue;
            }

            using var stream = File.OpenRead(file);
            result[i] = Texture2D.FromStream(GraphicsDevice, stream);
        }
        return result;
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
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _view.Draw(_batch);
        _batch.End();

        base.Draw(gameTime);
    }
}
