namespace Freuds.Core.Model;

/// <summary>
/// Solid / empty grid for the level. The world wraps horizontally. Below the last row is solid ground
/// and above the first row is open sky.
/// </summary>
public sealed class Terrain
{
    private readonly bool[,] _solid;

    public int Columns { get; }
    public int Rows { get; }
    public float CellSize { get; }

    public float WidthPx => Columns * CellSize;
    public float HeightPx => Rows * CellSize;

    /// <summary>Top edge, in pixels, of the highest solid cell. Equals HeightPx when there is no terrain.</summary>
    public float HighestTerrainY { get; }

    public Terrain(bool[,] solid, float cellSize)
    {
        if (cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        Columns = solid.GetLength(0);
        Rows = solid.GetLength(1);
        if (Columns == 0 || Rows == 0)
        {
            throw new ArgumentException("Terrain needs at least one cell.", nameof(solid));
        }

        CellSize = cellSize;
        _solid = (bool[,])solid.Clone();

        int top = Rows;
        for (int row = 0; row < Rows && top == Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                if (_solid[col, row])
                {
                    top = row;
                    break;
                }
            }
        }
        HighestTerrainY = top * cellSize;
    }

    /// <summary>Build terrain from text rows, where '#' is solid and any other character is empty.</summary>
    public static Terrain FromRows(IReadOnlyList<string> rows, float cellSize)
    {
        int rowCount = rows.Count;
        int colCount = rows.Max(r => r.Length);
        var solid = new bool[colCount, rowCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int col = 0; col < rows[row].Length; col++)
            {
                solid[col, row] = rows[row][col] == '#';
            }
        }
        return new Terrain(solid, cellSize);
    }

    /// <summary>Wrap an x position into [0, WidthPx).</summary>
    public float WrapX(float x)
    {
        float w = WidthPx;
        float result = x % w;
        return result < 0f ? result + w : result;
    }

    /// <summary>Shortest signed horizontal distance from one x to another on the looping world.</summary>
    public float WrapDelta(float from, float to)
    {
        float w = WidthPx;
        float d = (to - from) % w;
        if (d >= w / 2f)
        {
            d -= w;
        }
        else if (d < -w / 2f)
        {
            d += w;
        }
        return d;
    }

    public int RowAt(float y) => (int)MathF.Floor(y / CellSize);

    /// <summary>Column and row may be outside the grid: columns wrap, rows above are sky, rows below are ground.</summary>
    public bool IsSolidCell(int col, int row)
    {
        if (row < 0)
        {
            return false;
        }
        if (row >= Rows)
        {
            return true;
        }
        int wrapped = ((col % Columns) + Columns) % Columns;
        return _solid[wrapped, row];
    }

    public bool IsSolidAt(float x, float y) =>
        IsSolidCell((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));

    /// <summary>True if a circle overlaps any solid cell.</summary>
    public bool CircleHits(Vec2 center, float radius)
    {
        int minCol = (int)MathF.Floor((center.X - radius) / CellSize);
        int maxCol = (int)MathF.Floor((center.X + radius) / CellSize);
        int minRow = (int)MathF.Floor((center.Y - radius) / CellSize);
        int maxRow = (int)MathF.Floor((center.Y + radius) / CellSize);

        for (int col = minCol; col <= maxCol; col++)
        {
            for (int row = minRow; row <= maxRow; row++)
            {
                if (!IsSolidCell(col, row))
                {
                    continue;
                }

                // Closest point of the cell rectangle to the circle centre. Columns are unwrapped here
                // so the rectangle sits next to the circle even across the world seam.
                float x0 = col * CellSize;
                float y0 = row * CellSize;
                float nx = Math.Clamp(center.X, x0, x0 + CellSize);
                float ny = Math.Clamp(center.Y, y0, y0 + CellSize);
                float dx = center.X - nx;
                float dy = center.Y - ny;
                if (dx * dx + dy * dy < radius * radius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Placeholder landing-area rule: both feet of a ship at <paramref name="center"/> rest on solid cells in the
    /// same row, with open air above both. The real pad rule can change once levels mark pads explicitly.
    /// </summary>
    public bool IsFlatLandingSpot(Vec2 center, float radius)
    {
        float bottom = center.Y + radius;
        float left = center.X - radius * 0.6f;
        float right = center.X + radius * 0.6f;

        float probeY = bottom + 1.5f;
        if (!IsSolidAt(left, probeY) || !IsSolidAt(right, probeY))
        {
            return false;
        }

        // Both feet sample the same height, so they are in the same row. The surface is flat only if
        // the cells directly above that row are open at both feet (no step or wall).
        float rowTop = RowAt(probeY) * CellSize;
        return !IsSolidAt(left, rowTop - 1f) && !IsSolidAt(right, rowTop - 1f);
    }
}
