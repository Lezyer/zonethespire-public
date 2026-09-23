using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ZoneTheSpire.Core.Geometry;

public readonly record struct LabelBox(float X, float Y, float Width, float Height)
{
    public bool Overlaps(LabelBox other) =>
        X < other.X + other.Width && other.X < X + Width &&
        Y < other.Y + other.Height && other.Y < Y + Height;

    public float OverlapArea(LabelBox other) =>
        MathF.Max(0f, MathF.Min(X + Width, other.X + other.Width) - MathF.Max(X, other.X)) *
        MathF.Max(0f, MathF.Min(Y + Height, other.Y + other.Height) - MathF.Max(Y, other.Y));
}

/// <summary>
/// Places zone name labels as close to their anchor as possible without covering map node icons (obstacles) or
/// labels placed earlier in the same pass. When every candidate is blocked, the least-covered candidate wins.
/// </summary>
public static class LabelLayout
{
    private static readonly float[] HorizontalSteps = { 0f, -40f, 40f, -80f, 80f, -120f, 120f, -160f, 160f, -200f, 200f };
    private static readonly float[] VerticalSteps = { 0f, -24f, 24f, -48f, 48f, -72f, 72f, -96f, 96f, -120f, 120f };

    /// <summary>Candidate offsets ordered by distance from the anchor (ties: vertical, then horizontal step order).</summary>
    private static readonly Vector2[] CandidateOffsets = VerticalSteps
        .SelectMany((dy, row) => HorizontalSteps.Select((dx, column) => (Offset: new Vector2(dx, dy), row, column)))
        .OrderBy(c => c.Offset.LengthSquared())
        .ThenBy(c => c.row)
        .ThenBy(c => c.column)
        .Select(c => c.Offset)
        .ToArray();

    public static LabelBox Place(Vector2 anchor, Vector2 size, List<LabelBox> used, IReadOnlyList<LabelBox> obstacles)
    {
        LabelBox best = default;
        float bestCoverage = float.MaxValue;
        foreach (Vector2 offset in CandidateOffsets)
        {
            var candidate = new LabelBox(anchor.X + offset.X - size.X / 2f, anchor.Y + offset.Y - size.Y / 2f, size.X, size.Y);
            float coverage = Coverage(candidate, used) + Coverage(candidate, obstacles);
            if (coverage <= 0f)
            {
                used.Add(candidate);
                return candidate;
            }

            if (coverage < bestCoverage)
            {
                best = candidate;
                bestCoverage = coverage;
            }
        }

        used.Add(best);
        return best;
    }

    private static float Coverage(LabelBox candidate, IReadOnlyList<LabelBox> boxes)
    {
        float total = 0f;
        foreach (LabelBox box in boxes)
        {
            total += candidate.OverlapArea(box);
        }

        return total;
    }
}
