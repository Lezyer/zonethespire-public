using System;

namespace ZoneTheSpire.Core.Model;

/// <summary>Engine-free map coordinate. Ordered by row, then column (the canonical deterministic order).</summary>
public readonly record struct GridCoord(int Col, int Row) : IComparable<GridCoord>
{
    public int CompareTo(GridCoord other)
    {
        int byRow = Row.CompareTo(other.Row);
        return byRow != 0 ? byRow : Col.CompareTo(other.Col);
    }

    /// <summary>True for the up-to-8 surrounding grid cells (never for itself).</summary>
    public bool IsAdjacentTo(GridCoord other) =>
        this != other && Math.Abs(Row - other.Row) <= 1 && Math.Abs(Col - other.Col) <= 1;
}
