using Freuds.Core.Model;

namespace Freuds.Core.Levels;

/// <summary>
/// Hard-coded test level for milestone 1, shaped like the TileMap example: 96 by 32 cells of 32 px,
/// a floating island whose top row is row 6, and ground along the bottom. It is replaced by the
/// TileMap importer in milestone 3.
/// </summary>
public static class DemoLevel
{
    public const int Columns = 96;
    public const int Rows = 32;
    public const float CellSize = 32f;

    public static Vec2 PlayerStart => new(144f, 400f);

    /// <summary>Level 1 physics: half the default gravity and 20 percent more engine power.</summary>
    public static LevelSettings Settings
    {
        get
        {
            var baseline = new GameConfig();
            return new LevelSettings(
                Gravity: baseline.Gravity * 0.5f,
                ThrustAccel: baseline.ThrustAccel * 1.2f);
        }
    }

    public static Terrain Create()
    {
        var solid = new bool[Columns, Rows];

        // Ground: three rows along the bottom, flat all the way round so the world loops cleanly.
        for (int col = 0; col < Columns; col++)
        {
            for (int row = 29; row < Rows; row++)
            {
                solid[col, row] = true;
            }
        }

        // A raised mesa with a flat top to practise landing on.
        Fill(solid, 18, 28, 26, 28);

        // A wall to practise crashing into.
        Fill(solid, 40, 17, 42, 28);

        // A floating island, highest terrain point (row 6).
        Fill(solid, 52, 6, 55, 6);
        Fill(solid, 51, 7, 55, 7);
        Fill(solid, 50, 8, 55, 8);
        Fill(solid, 50, 9, 51, 9);
        Fill(solid, 54, 9, 55, 9);
        Fill(solid, 55, 10, 57, 10);

        // A slope on the left edge, and a matching one on the right so the seam joins.
        for (int i = 0; i < 6; i++)
        {
            Fill(solid, 0, 28 - i, i, 28 - i);
            Fill(solid, Columns - 1 - i, 28 - i, Columns - 1, 28 - i);
        }

        return new Terrain(solid, CellSize);
    }

    private static void Fill(bool[,] solid, int col0, int row0, int col1, int row1)
    {
        for (int col = col0; col <= col1; col++)
        {
            for (int row = row0; row <= row1; row++)
            {
                solid[col, row] = true;
            }
        }
    }
}
