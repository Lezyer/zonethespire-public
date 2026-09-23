using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Mirror;

/// <summary>A creature's horizontal footprint in the enemy container: centre X and width.</summary>
public readonly record struct CreatureSpan(float CenterX, float Width);

/// <summary>A target position: centre X and a vertical offset from the row baseline (negative is up).</summary>
public readonly record struct RowSlot(float CenterX, float YOffset);

/// <summary>
/// A creature at position (<see cref="CenterX"/>, <see cref="Y"/>) whose visual bounds span
/// [Y + TopOffset, Y + TopOffset + Height] vertically (negative Y is up).
/// </summary>
public readonly record struct CreatureBox(float CenterX, float Y, float Width, float TopOffset, float Height)
{
    public float Top => Y + TopOffset;

    public CreatureSpan Span => new(CenterX, Width);
}

/// <summary>The clone's size, with bounds relative to its position like <see cref="CreatureBox"/>.</summary>
public readonly record struct CloneShape(float Width, float TopOffset, float Height);

/// <summary>
/// On-screen limits in enemy container coordinates: centre-X range edges and the highest allowed bounds top.
/// <see cref="SpreadMinX"/> is how far left a row may reach when the usual range is too tight: up to the players' right edge
/// (plus a gap) or the screen's left edge, whichever is nearer.
/// </summary>
public readonly record struct StageBounds(float MinX, float MaxX, float MinTop, float SpreadMinX = CloneLayout.SpreadMinX);

public enum ClonePlanKind
{
    /// <summary>Only the clone moves, to (<see cref="ClonePlan.X"/>, <see cref="ClonePlan.Y"/>).</summary>
    Place,

    /// <summary>The whole row moves to <see cref="ClonePlan.Row"/>, with the clone inserted at <see cref="ClonePlan.CloneIndex"/>.</summary>
    Reflow,
}

public sealed record ClonePlan(ClonePlanKind Kind, float X, float Y, IReadOnlyList<RowSlot> Row, int CloneIndex)
{
    public static ClonePlan At(float x, float y) => new(ClonePlanKind.Place, x, y, Array.Empty<RowSlot>(), -1);

    public static ClonePlan Reflowed(IReadOnlyList<RowSlot> row, int cloneIndex) => new(ClonePlanKind.Reflow, 0f, 0f, row, cloneIndex);
}

/// <summary>
/// Where Mirrorlands clones go in the enemy row. Rendering-only (each peer lays out its own screen), no RNG.
/// Coordinates match NCombatRoom.PositionEnemies: centre X in [MinX, maxX], maxX = 960 / camera scaling.
/// </summary>
public static class CloneLayout
{
    public const float MinX = 150f;
    public const float CloneGap = 40f;
    public const float RowGap = 70f;
    public const float StaggerY = 60f;

    /// <summary>Space kept between a floating clone's bounds and the top of the creature below (room for its intent).</summary>
    public const float FloatGap = 120f;

    /// <summary>The row's ground line (NCombatRoom's enemy baseline): creatures with their feet here stand on the ground.</summary>
    public const float GroundY = 200f;

    /// <summary>Tighter float clearances tried, in order, when the full <see cref="FloatGap"/> would leave the screen.</summary>
    private static readonly float[] FloatGaps = { FloatGap, FloatGap / 2f, 20f };

    private const float GroundTolerance = 1f;

    /// <summary>Default <see cref="StageBounds.SpreadMinX"/> when the players' position isn't known: the middle of the screen.</summary>
    public const float SpreadMinX = 0f;

    /// <summary>The smallest gap between neighbours in a grounded row: they never overlap.</summary>
    private const float MinGroundGap = 5f;

    private const float Epsilon = 0.01f;

    /// <summary>
    /// The on-screen X nearest the original (right side first on ties) whose clone footprint keeps
    /// <see cref="CloneGap"/> from every occupied span, or null when the row has no such gap.
    /// <paramref name="occupied"/> should include the original.
    /// </summary>
    public static float? FindFreeX(CreatureSpan original, float cloneWidth, IReadOnlyList<CreatureSpan> occupied, float maxX) =>
        FindFreeX(original, cloneWidth, occupied, MinX, maxX);

    /// <summary>
    /// Final on-screen guard for a creature of <paramref name="shape"/> with its feet at (x, y): its footprint stays within
    /// the stage horizontally (centred when wider than the stage) and its top stays below the stage top.
    /// </summary>
    public static (float X, float Y) ClampOnStage(float x, float y, CloneShape shape, StageBounds stage)
    {
        float half = shape.Width / 2f;
        float low = stage.MinX + half;
        float high = MathF.Max(low, stage.MaxX - half);
        return (Math.Clamp(x, low, high), MathF.Max(y, stage.MinTop - shape.TopOffset));
    }

    /// <summary>
    /// Plans a clone's spot, never off screen and never overlapping when there is any way around it, in this order:
    /// <list type="number">
    /// <item>the clone alone takes a free gap in the row, on the ground;</item>
    /// <item>the whole row is re-laid like vanilla (centred, gaps shrinking to no less than a few pixels, every second
    /// creature raised when tight), with the clone next to its original. Creatures standing on the ground stay on it;
    /// hand-placed ones in the air keep their height. Only if every creature stays on screen;</item>
    /// <item>the row laid on the ground over the wider space up to the players (<see cref="StageBounds.SpreadMinX"/>),
    /// right-aligned, still without overlap;</item>
    /// <item>the clone floats above the creatures below it, clear of their intents (the clearance shrinks before giving up);</item>
    /// <item>overlap can't be avoided (giants such as two Byrdonis): the row is spread as wide as the screen allows, from
    /// <see cref="StageBounds.SpreadMinX"/> to the right edge, so creatures overlap as little as possible;</item>
    /// <item>last resort: the on-screen spot with the least overlap, raised by <see cref="StaggerY"/>.</item>
    /// </list>
    /// <paramref name="occupied"/> is the row left to right and should include the original.
    /// </summary>
    public static ClonePlan PlanClone(CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied, StageBounds stage)
    {
        float half = clone.Width / 2f;
        List<CreatureSpan> spans = occupied.Select(box => box.Span).ToList();

        if (FindFreeX(original.Span, clone.Width, spans, stage.MinX, stage.MaxX) is float freeX
            && original.Y + clone.TopOffset >= stage.MinTop - Epsilon)
        {
            return ClonePlan.At(freeX, original.Y);
        }

        if (GroundedRow(original, clone, occupied, stage) is { } row)
        {
            return row;
        }

        if (WideGroundRow(original, clone, occupied, stage) is { } wide)
        {
            return wide;
        }

        foreach (float gap in FloatGaps)
        {
            if (FindFloatingSpot(original, clone, occupied, stage, gap) is { } floating)
            {
                return ClonePlan.At(floating.X, floating.Y);
            }
        }

        if (SpreadRow(original, clone, occupied, stage) is { } spread)
        {
            return spread;
        }

        float low = stage.MinX + half;
        float high = MathF.Max(low, stage.MaxX - half);
        float y = MathF.Max(stage.MinTop - clone.TopOffset, original.Y - StaggerY);
        float bestX = low;
        float bestOverlap = float.MaxValue;
        foreach (float candidate in Candidates(original.Span, half, spans))
        {
            float x = Math.Clamp(candidate, low, high);
            float overlap = occupied.Sum(box => OverlapArea(x, y, clone, box));
            if (overlap < bestOverlap - Epsilon)
            {
                bestOverlap = overlap;
                bestX = x;
            }
        }

        return ClonePlan.At(bestX, y);
    }

    /// <summary>
    /// The whole row re-laid with the clone after its original (see <see cref="Reflow"/>), or null when any creature would
    /// leave the screen. A creature off the ground (hand-placed in the air) keeps its height, and the clone takes its
    /// original's; the rest stand on the ground with the reflow's stagger. <see cref="Reflow"/> never lets neighbours overlap.
    /// </summary>
    private static ClonePlan? GroundedRow(CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied, StageBounds stage)
    {
        (List<float> widths, List<float> tops, List<float?> lifts, int cloneIndex) = Assemble(original, clone, occupied);
        IReadOnlyList<RowSlot> reflow = Reflow(widths, stage.MaxX);
        var row = reflow.Select((slot, i) => new RowSlot(slot.CenterX, lifts[i] ?? slot.YOffset)).ToList();
        bool fits = row.Select((slot, i) => (slot, i)).All(entry =>
            entry.slot.CenterX - widths[entry.i] / 2f >= stage.MinX / 2f - Epsilon
            && entry.slot.CenterX + widths[entry.i] / 2f <= stage.MaxX + Epsilon
            && GroundY + entry.slot.YOffset + tops[entry.i] >= stage.MinTop - Epsilon);
        return fits ? ClonePlan.Reflowed(row, cloneIndex) : null;
    }

    /// <summary>
    /// The row with the clone inserted, left to right: widths, bounds top offsets and lifts (see <see cref="Lift"/>). A clone
    /// goes right after its original; a newcomer that isn't in the row (a slotted summon) goes where it stands. The clone
    /// takes its original's lift.
    /// </summary>
    private static (List<float> Widths, List<float> Tops, List<float?> Lifts, int CloneIndex) Assemble(
        CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied)
    {
        int originalIndex = IndexOf(occupied, original);
        int cloneIndex = originalIndex >= 0 ? originalIndex + 1 : occupied.Count(box => box.CenterX < original.CenterX);
        var widths = occupied.Select(box => box.Width).ToList();
        var tops = occupied.Select(box => box.TopOffset).ToList();
        var lifts = occupied.Select(Lift).ToList();
        widths.Insert(cloneIndex, clone.Width);
        tops.Insert(cloneIndex, clone.TopOffset);
        lifts.Insert(cloneIndex, Lift(original));
        return (widths, tops, lifts, cloneIndex);
    }

    /// <summary>
    /// The row on the ground over the wider space (<see cref="StageBounds.SpreadMinX"/> to the right edge), right-aligned so it
    /// keeps as far from the players as it can, with vanilla gaps shrinking to <see cref="MinGroundGap"/> (and vanilla's
    /// stagger when tight). Null when even that would make neighbours overlap.
    /// </summary>
    private static ClonePlan? WideGroundRow(CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied, StageBounds stage)
    {
        (List<float> widths, List<float> tops, List<float?> lifts, int cloneIndex) = Assemble(original, clone, occupied);
        if (widths.Count < 2)
        {
            return null;
        }

        float total = widths.Sum();
        float gap = MathF.Min(RowGap, (stage.MaxX - stage.SpreadMinX - total) / (widths.Count - 1));
        if (gap < MinGroundGap - Epsilon)
        {
            return null;
        }

        float stagger = gap < 30f ? float.Lerp(60f, 40f, (gap - 5f) / 25f) : 0f;
        var row = new List<RowSlot>(widths.Count);
        float x = stage.MaxX - total - gap * (widths.Count - 1);
        for (int i = 0; i < widths.Count; i++)
        {
            float raise = i % 2 != 0 && GroundY - stagger + tops[i] >= stage.MinTop - Epsilon ? -stagger : 0f;
            row.Add(new RowSlot(x + widths[i] / 2f, lifts[i] ?? raise));
            x += widths[i] + gap;
        }

        return ClonePlan.Reflowed(row, cloneIndex);
    }

    /// <summary>
    /// The row spread evenly from <see cref="StageBounds.SpreadMinX"/> to the right edge, neighbours overlapping as little as the screen
    /// allows. Every second grounded creature is raised by <see cref="StaggerY"/> (vanilla's crowded look) when that keeps
    /// it on screen; creatures in the air keep their height.
    /// </summary>
    private static ClonePlan? SpreadRow(CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied, StageBounds stage)
    {
        (List<float> widths, List<float> tops, List<float?> lifts, int cloneIndex) = Assemble(original, clone, occupied);
        if (widths.Count < 2)
        {
            return null;
        }

        float gap = (stage.MaxX - stage.SpreadMinX - widths.Sum()) / (widths.Count - 1);
        var row = new List<RowSlot>(widths.Count);
        float x = stage.SpreadMinX;
        for (int i = 0; i < widths.Count; i++)
        {
            float raise = i % 2 != 0 && GroundY - StaggerY + tops[i] >= stage.MinTop - Epsilon ? -StaggerY : 0f;
            row.Add(new RowSlot(x + widths[i] / 2f, lifts[i] ?? raise));
            x += widths[i] + gap;
        }

        return ClonePlan.Reflowed(row, cloneIndex);
    }

    /// <summary>How far above the ground a creature stands (negative), or null when it stands on the ground.</summary>
    private static float? Lift(CreatureBox box) => MathF.Abs(box.Y - GroundY) <= GroundTolerance ? null : box.Y - GroundY;

    /// <summary>
    /// Lays out a whole row (widths in left-to-right order) the way NCombatRoom.PositionEnemies does: centred with 70px
    /// gaps, compressed to fit the screen when crowded, with every second creature raised when the gaps get tight.
    /// </summary>
    public static IReadOnlyList<RowSlot> Reflow(IReadOnlyList<float> widths, float maxX)
    {
        var slots = new List<RowSlot>(widths.Count);
        if (widths.Count == 0)
        {
            return slots;
        }

        float gap = RowGap;
        float total = widths.Sum();
        float span = total + (widths.Count - 1) * gap;
        float x = MathF.Max((maxX - span) * 0.5f, MinX);
        float stagger = 0f;
        if (x + span > maxX && widths.Count > 1)
        {
            gap = MathF.Max((maxX - MinX - total) / (widths.Count - 1), 5f);
            span = total + (widths.Count - 1) * gap;
            x = (maxX - span) * 0.5f;
            if (gap < 30f)
            {
                stagger = float.Lerp(60f, 40f, (gap - 5f) / 25f);
            }
        }

        for (int i = 0; i < widths.Count; i++)
        {
            slots.Add(new RowSlot(x + widths[i] * 0.5f, i % 2 != 0 ? -stagger : 0f));
            x += widths[i] + gap;
        }

        return slots;
    }

    private static float? FindFreeX(CreatureSpan original, float cloneWidth, IReadOnlyList<CreatureSpan> occupied, float minX, float maxX)
    {
        float half = cloneWidth / 2f;
        foreach (float x in Candidates(original, half, occupied))
        {
            bool onScreen = x - half >= minX - Epsilon && x + half <= maxX + Epsilon;
            if (onScreen && occupied.All(o => MathF.Abs(x - o.CenterX) >= half + o.Width / 2f + CloneGap - Epsilon))
            {
                return x;
            }
        }

        return null;
    }

    /// <summary>
    /// The on-screen spot nearest the original where the clone sits <paramref name="gap"/> above every creature it overlaps
    /// horizontally, never lower than the original. Null if every such spot pokes above the screen.
    /// </summary>
    private static (float X, float Y)? FindFloatingSpot(CreatureBox original, CloneShape clone, IReadOnlyList<CreatureBox> occupied, StageBounds stage, float gap)
    {
        float half = clone.Width / 2f;
        // Spots beside each creature, over each creature, and at both screen edges (the only ones left when wide creatures
        // fill the row).
        IEnumerable<float> xs = Candidates(original.Span, half, occupied.Select(box => box.Span).ToList())
            .Concat(occupied.Select(box => box.CenterX))
            .Append(original.CenterX)
            .Append(stage.MinX + half)
            .Append(stage.MaxX - half);

        (float X, float Y)? best = null;
        float bestScore = float.MaxValue;
        foreach (float x in xs)
        {
            if (x - half < stage.MinX - Epsilon || x + half > stage.MaxX + Epsilon)
            {
                continue;
            }

            float y = original.Y;
            foreach (CreatureBox box in occupied)
            {
                if (MathF.Abs(x - box.CenterX) < half + box.Width / 2f)
                {
                    y = MathF.Min(y, box.Top - gap - clone.TopOffset - clone.Height);
                }
            }

            if (y + clone.TopOffset < stage.MinTop - Epsilon)
            {
                continue;
            }

            float score = MathF.Abs(x - original.CenterX) + MathF.Abs(y - original.Y);
            bool better = score < bestScore - Epsilon
                          || (MathF.Abs(score - bestScore) <= Epsilon && best is { } current && x > current.X && x >= original.CenterX);
            if (better)
            {
                bestScore = score;
                best = (x, y);
            }
        }

        return best;
    }

    private static int IndexOf(IReadOnlyList<CreatureBox> boxes, CreatureBox target)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i] == target)
            {
                return i;
            }
        }

        return -1;
    }

    private static IEnumerable<float> Candidates(CreatureSpan original, float half, IReadOnlyList<CreatureSpan> occupied) =>
        occupied.Prepend(original)
            .SelectMany(o => new[]
            {
                o.CenterX + o.Width / 2f + CloneGap + half,
                o.CenterX - o.Width / 2f - CloneGap - half,
            })
            .OrderBy(x => MathF.Abs(x - original.CenterX))
            .ThenBy(x => x < original.CenterX ? 1 : 0)
            .ToList();

    private static float OverlapArea(float x, float y, CloneShape clone, CreatureBox box)
    {
        float width = MathF.Max(0f, MathF.Min(x + clone.Width / 2f, box.CenterX + box.Width / 2f) - MathF.Max(x - clone.Width / 2f, box.CenterX - box.Width / 2f));
        float cloneTop = y + clone.TopOffset;
        float height = MathF.Max(0f, MathF.Min(cloneTop + clone.Height, box.Top + box.Height) - MathF.Max(cloneTop, box.Top));
        return width * height;
    }
}
