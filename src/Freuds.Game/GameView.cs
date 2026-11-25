using Freuds.Core;
using Freuds.Core.Events;
using Freuds.Core.Levels;
using Freuds.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Freuds.Game;

/// <summary>
/// Draws the model with placeholder shapes. On every tick it rebuilds its sprites from the model's
/// collections of moving things. The model never sends messages to the view.
/// </summary>
public sealed class GameView
{
    private readonly World _world;
    private readonly Texture2D _pixel;
    private readonly int _viewW;
    private readonly int _viewH;
    private readonly SpritePool _sprites = new();

    private readonly LevelData? _level;
    private readonly Texture2D?[] _tilesets;

    private float _camCenterX;
    private float _camY;
    private bool _camReady;

    /// <param name="level">Optional imported level, used for background art and unit markers.</param>
    /// <param name="tilesets">One texture per tileset in the level. An entry may be null if its image is missing.</param>
    public GameView(EventManager events, World world, Texture2D pixel, int viewWidth, int viewHeight,
        LevelData? level = null, Texture2D?[]? tilesets = null)
    {
        _level = level;
        _tilesets = tilesets ?? Array.Empty<Texture2D?>();
        _world = world;
        _pixel = pixel;
        _viewW = viewWidth;
        _viewH = viewHeight;

        // The model subscribed first, so by the time this runs the model has finished its update.
        events.Subscribe<TickEvent>(_ => Rebuild());
        Rebuild();
    }

    private void Rebuild()
    {
        var player = _world.Player;

        _sprites.Reset();

        var ship = _sprites.Next();
        ship.Kind = SpriteKind.Ship;
        ship.Position = player.Position;
        ship.Angle = player.Angle;
        ship.Thrusting = player.Thrusting;
        ship.ShipLook = player.State switch
        {
            ShipState.Landed => ShipLook.Landed,
            ShipState.Crashed => ShipLook.Crashed,
            _ => ShipLook.Flying,
        };

        foreach (var bolt in _world.Bolts)
        {
            var sprite = _sprites.Next();
            sprite.Kind = SpriteKind.Bolt;
            sprite.Position = bolt.Position;
            sprite.Angle = bolt.Angle;
        }

        // Horizontal scrolling follows the ship round the looping world. Vertical scrolling stops at the
        // lowest ground, and stops again once the ship is high enough for the mother ship to appear.
        _camCenterX = player.Position.X;
        if (!_camReady || !_world.AtMotherShipHeight)
        {
            float target = player.Position.Y - _viewH / 2f;
            float lowest = _world.Terrain.HeightPx - _viewH;
            _camY = MathF.Min(target, lowest);
            _camReady = true;
        }
    }

    public void Draw(SpriteBatch batch)
    {
        DrawBackground(batch);
        if (!HasArt)
        {
            DrawTerrain(batch);
        }
        DrawUnitMarkers(batch);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var sprite = _sprites[i];
            switch (sprite.Kind)
            {
                case SpriteKind.Ship:
                    DrawShip(batch, sprite);
                    break;
                case SpriteKind.Bolt:
                    DrawBolt(batch, sprite);
                    break;
            }
        }

        DrawHud(batch);
    }

    // With imported art the picture shows the ground, so the placeholder terrain blocks are only drawn without it.
    private bool HasArt => _level is { Background.Count: > 0 } && _tilesets.Any(t => t is not null);

    private void DrawBackground(SpriteBatch batch)
    {
        if (_level is null)
        {
            return;
        }

        var terrain = _world.Terrain;
        float left = _camCenterX - _viewW / 2f;

        foreach (var tile in _level.Background)
        {
            if (tile.Tileset >= _tilesets.Length || _tilesets[tile.Tileset] is not { } texture)
            {
                continue;
            }

            if (tile.Y + tile.Height < _camY || tile.Y > _camY + _viewH)
            {
                continue;
            }

            // The world loops, so find where this tile appears nearest the camera.
            float dx = terrain.WrapDelta(_camCenterX, tile.X + tile.Width / 2f) - tile.Width / 2f;
            float x = _viewW / 2f + dx;
            if (x + tile.Width < 0 || x > _viewW)
            {
                continue;
            }

            var set = _level.Tilesets[tile.Tileset];
            int srcX = tile.TileIndex % set.Columns * set.TileWidth;
            int srcY = tile.TileIndex / set.Columns * set.TileHeight;

            var effects = SpriteEffects.None;
            if (tile.FlipHorizontal)
            {
                effects |= SpriteEffects.FlipHorizontally;
            }
            if (tile.FlipVertical)
            {
                effects |= SpriteEffects.FlipVertically;
            }

            batch.Draw(
                texture,
                new Rectangle((int)MathF.Round(x), (int)MathF.Round(tile.Y - _camY), (int)tile.Width, (int)tile.Height),
                new Rectangle(srcX, srcY, set.TileWidth, set.TileHeight),
                Color.White,
                0f,
                new Vector2(0f, 0f),
                effects,
                0f);
        }
    }

    /// <summary>Placeholder markers for spawn points until each unit has its own art and behaviour.</summary>
    private void DrawUnitMarkers(SpriteBatch batch)
    {
        if (_level is null)
        {
            return;
        }

        foreach (var unit in _level.Units)
        {
            float x = ScreenX(unit.Position.X);
            float y = ScreenY(unit.Position.Y);
            if (x < -20 || x > _viewW + 20 || y < -20 || y > _viewH + 20)
            {
                continue;
            }

            var color = unit.Type switch
            {
                UnitType.Prison => Color.LimeGreen,
                UnitType.FuelDump => Color.Cyan,
                UnitType.Graviton or UnitType.Repulsor => Color.Magenta,
                UnitType.Volcano => Color.Orange,
                _ => Color.Red,
            };
            batch.Draw(_pixel, new Rectangle((int)x - 6, (int)y - 6, 12, 12), color);
        }
    }

    private float ScreenX(float worldX) => _viewW / 2f + _world.Terrain.WrapDelta(_camCenterX, worldX);

    private float ScreenY(float worldY) => worldY - _camY;

    private void DrawTerrain(SpriteBatch batch)
    {
        var terrain = _world.Terrain;
        int size = (int)terrain.CellSize;
        float left = _camCenterX - _viewW / 2f;

        int colMin = (int)MathF.Floor(left / size);
        int colMax = (int)MathF.Floor((left + _viewW) / size);
        int rowMin = Math.Max(0, (int)MathF.Floor(_camY / size));
        int rowMax = Math.Min(terrain.Rows - 1, (int)MathF.Floor((_camY + _viewH) / size));

        var fill = new Color(70, 74, 92);
        var edge = new Color(150, 160, 190);

        for (int col = colMin; col <= colMax; col++)
        {
            for (int row = rowMin; row <= rowMax; row++)
            {
                if (!terrain.IsSolidCell(col, row))
                {
                    continue;
                }

                int x = (int)MathF.Round(col * size - left);
                int y = (int)MathF.Round(row * size - _camY);
                batch.Draw(_pixel, new Rectangle(x, y, size, size), fill);

                if (!terrain.IsSolidCell(col, row - 1))
                {
                    batch.Draw(_pixel, new Rectangle(x, y, size, 3), edge);
                }
            }
        }
    }

    private void DrawShip(SpriteBatch batch, Sprite sprite)
    {
        float r = _world.Config.ShipRadius;
        var color = sprite.ShipLook switch
        {
            ShipLook.Landed => Color.LimeGreen,
            ShipLook.Crashed => Color.Red,
            _ => Color.White,
        };

        var centre = new Vector2(ScreenX(sprite.Position.X), ScreenY(sprite.Position.Y));
        var nose = centre + Rotate(new Vector2(0f, -r * 1.3f), sprite.Angle);
        var left = centre + Rotate(new Vector2(-r * 0.9f, r), sprite.Angle);
        var right = centre + Rotate(new Vector2(r * 0.9f, r), sprite.Angle);

        DrawLine(batch, nose, left, color, 2f);
        DrawLine(batch, left, right, color, 2f);
        DrawLine(batch, right, nose, color, 2f);

        if (sprite.Thrusting)
        {
            var baseMid = centre + Rotate(new Vector2(0f, r), sprite.Angle);
            var flame = centre + Rotate(new Vector2(0f, r * 2.2f), sprite.Angle);
            DrawLine(batch, baseMid, flame, Color.Orange, 4f);
        }
    }

    private void DrawBolt(SpriteBatch batch, Sprite sprite)
    {
        float size = _world.Config.BoltRadius * 2f;
        float x = ScreenX(sprite.Position.X) - size / 2f;
        float y = ScreenY(sprite.Position.Y) - size / 2f;
        batch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)size, (int)size), Color.Yellow);
    }

    private void DrawHud(SpriteBatch batch)
    {
        var player = _world.Player;
        var cfg = _world.Config;

        // Fuel bar.
        batch.Draw(_pixel, new Rectangle(16, 16, 204, 16), new Color(40, 40, 50));
        int fuelWidth = (int)(200f * player.Fuel / cfg.MaxFuel);
        batch.Draw(_pixel, new Rectangle(18, 18, fuelWidth, 12), Color.Cyan);

        // One pip per bolt, dimmed while that bolt is in flight.
        int inFlight = _world.Bolts.Count;
        for (int i = 0; i < cfg.MaxBolts; i++)
        {
            var pip = i < cfg.MaxBolts - inFlight ? Color.Yellow : new Color(60, 60, 30);
            batch.Draw(_pixel, new Rectangle(16 + i * 10, 40, 8, 8), pip);
        }

        // Placeholder for the mother ship appearing.
        if (_world.AtMotherShipHeight)
        {
            batch.Draw(_pixel, new Rectangle(_viewW / 2 - 100, 16, 200, 12), Color.Magenta);
        }

        if (player.State == ShipState.Crashed)
        {
            var red = new Color(200, 30, 30);
            batch.Draw(_pixel, new Rectangle(0, 0, _viewW, 6), red);
            batch.Draw(_pixel, new Rectangle(0, _viewH - 6, _viewW, 6), red);
            batch.Draw(_pixel, new Rectangle(0, 0, 6, _viewH), red);
            batch.Draw(_pixel, new Rectangle(_viewW - 6, 0, 6, _viewH), red);
        }
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private void DrawLine(SpriteBatch batch, Vector2 a, Vector2 b, Color color, float thickness)
    {
        var d = b - a;
        float length = d.Length();
        if (length < 0.01f)
        {
            return;
        }
        float angle = MathF.Atan2(d.Y, d.X);
        batch.Draw(_pixel, a, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }
}
