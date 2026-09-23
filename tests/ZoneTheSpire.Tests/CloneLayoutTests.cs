using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Mirror;
using Xunit;

namespace ZoneTheSpire.Tests;

public class CloneLayoutTests
{
    private const float MaxX = 960f;
    private const float Baseline = 200f;

    // Creatures in these tests are 200px tall with their feet at Y (bounds from Y-200 to Y).
    private static CreatureBox Box(float centerX, float width, float y = Baseline) => new(centerX, y, width, -200f, 200f);

    private static CloneShape Shape(float width) => new(width, -200f, 200f);

    [Fact]
    public void FindFreeX_PlacesRightOfOriginal_WhenClear()
    {
        var original = new CreatureSpan(400f, 200f);

        Assert.Equal(640f, CloneLayout.FindFreeX(original, 200f, new[] { original }, 1000f));
    }

    [Fact]
    public void FindFreeX_SkipsSpotCoveredByNeighbour()
    {
        // Vanilla spacing: two 200px enemies 70px apart. Right of A lands on B; left of A is off-screen.
        var a = new CreatureSpan(300f, 200f);
        var b = new CreatureSpan(570f, 200f);

        Assert.Equal(810f, CloneLayout.FindFreeX(a, 200f, new[] { a, b }, MaxX));
    }

    [Fact]
    public void FindFreeX_AvoidsEarlierClone()
    {
        var original = new CreatureSpan(400f, 200f);
        var earlierClone = new CreatureSpan(640f, 200f);

        Assert.Equal(880f, CloneLayout.FindFreeX(original, 200f, new[] { original, earlierClone }, 1000f));
    }

    [Fact]
    public void FindFreeX_PrefersNearestFreeSpot_LeftBeforeFarRight()
    {
        var neighbour = new CreatureSpan(820f, 200f);
        var original = new CreatureSpan(600f, 200f);

        // Right of original (840) is covered; left of original (360) is 240 away; right of neighbour (1060) is off-screen.
        Assert.Equal(360f, CloneLayout.FindFreeX(original, 200f, new[] { original, neighbour }, 1000f));
    }

    [Fact]
    public void FindFreeX_ReturnsNull_WhenRowIsFull()
    {
        var a = new CreatureSpan(300f, 250f);
        var b = new CreatureSpan(560f, 250f);
        var c = new CreatureSpan(820f, 250f);

        Assert.Null(CloneLayout.FindFreeX(b, 250f, new[] { a, b, c }, MaxX));
    }

    [Fact]
    public void Reflow_SingleCreature_IsCentred()
    {
        IReadOnlyList<RowSlot> slots = CloneLayout.Reflow(new[] { 200f }, MaxX);

        Assert.Equal(new[] { new RowSlot(480f, 0f) }, slots);
    }

    [Fact]
    public void Reflow_MatchesVanillaSpacing_WhenRoomy()
    {
        IReadOnlyList<RowSlot> slots = CloneLayout.Reflow(new[] { 200f, 200f }, MaxX);

        Assert.Equal(new[] { new RowSlot(345f, 0f), new RowSlot(615f, 0f) }, slots);
    }

    [Fact]
    public void Reflow_CompressesAndStaggers_WhenCrowded()
    {
        IReadOnlyList<RowSlot> slots = CloneLayout.Reflow(new[] { 250f, 250f, 250f, 250f }, MaxX);

        Assert.Equal(
            new[] { new RowSlot(97.5f, 0f), new RowSlot(352.5f, -60f), new RowSlot(607.5f, 0f), new RowSlot(862.5f, -60f) },
            slots);
    }

    [Fact]
    public void PlanClone_UsesFreeRowGap_WhenAvailable()
    {
        CreatureBox original = Box(400f, 200f);

        ClonePlan plan = CloneLayout.PlanClone(original, Shape(200f), new[] { original }, new StageBounds(150f, 1000f, -430f));

        Assert.Equal(ClonePlanKind.Place, plan.Kind);
        Assert.Equal(640f, plan.X);
        Assert.Equal(Baseline, plan.Y);
    }

    [Fact]
    public void PlanClone_ReflowsRow_WhenNoGapButTheGroundedRowStillFits()
    {
        CreatureBox a = Box(250f, 150f);
        CreatureBox b = Box(420f, 150f);
        CreatureBox c = Box(590f, 150f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(150f), new[] { a, b, c }, new StageBounds(150f, 800f, -250f));

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(4, plan.Row.Count);
        Assert.Equal(1, plan.CloneIndex);
        Assert.Equal(150f, plan.Row[0].CenterX, 2);
        Assert.Equal(650f, plan.Row[3].CenterX, 2);
    }

    [Fact]
    public void PlanClone_PrefersTheGroundedRowOverFloating()
    {
        CreatureBox a = Box(250f, 150f);
        CreatureBox b = Box(420f, 150f);
        CreatureBox c = Box(590f, 150f);

        // A floating spot exists (MinTop -430 leaves room above the row), but the row still fits on the ground.
        ClonePlan plan = CloneLayout.PlanClone(a, Shape(150f), new[] { a, b, c }, new StageBounds(150f, 800f, -430f));

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(1, plan.CloneIndex);
    }

    [Fact]
    public void PlanClone_Reflow_KeepsAnElevatedCreatureAtItsHeight()
    {
        // A hand-placed flyer 150px above the ground keeps that height when the row is re-laid; grounded ones stay grounded.
        CreatureBox a = Box(250f, 150f);
        CreatureBox flyer = Box(420f, 150f, Baseline - 150f);
        CreatureBox c = Box(590f, 150f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(150f), new[] { a, flyer, c }, new StageBounds(150f, 800f, -430f));

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(0f, plan.Row[0].YOffset);
        Assert.Equal(-150f, plan.Row[2].YOffset);
    }

    [Fact]
    public void PlanClone_InsertsANewcomerByItsPosition_WhenItIsNotPartOfTheRow()
    {
        // A slotted summon arriving between a and b (not in the row itself): the re-laid row keeps it between them.
        CreatureBox a = Box(250f, 150f);
        CreatureBox b = Box(420f, 150f);
        CreatureBox c = Box(590f, 150f);
        CreatureBox summon = Box(330f, 150f);

        ClonePlan plan = CloneLayout.PlanClone(summon, Shape(150f), new[] { a, b, c }, new StageBounds(150f, 800f, -250f));

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(1, plan.CloneIndex);
    }

    [Fact]
    public void PlanClone_FloatsTheClone_WhenTheRowCannotFitOnTheGround()
    {
        CreatureBox a = Box(300f, 250f);
        CreatureBox b = Box(560f, 250f);
        CreatureBox c = Box(820f, 250f);

        ClonePlan plan = CloneLayout.PlanClone(b, Shape(250f), new[] { a, b, c }, new StageBounds(150f, MaxX, -430f));

        // Clone bottom sits FloatGap above b's top (0): y = 0 - 120 - (-200) - 200 = -120.
        Assert.Equal(ClonePlanKind.Place, plan.Kind);
        Assert.Equal(560f, plan.X);
        Assert.Equal(-120f, plan.Y);
    }

    [Fact]
    public void PlanClone_UsesTheSpaceTowardThePlayer_BeforeFloating()
    {
        // Two wide creatures (e.g. Toadpoles): a third doesn't fit in the usual enemy area (885px), but does once the row may
        // reach toward the player (960px from SpreadMinX 0): it stays on the ground, right-aligned, no overlap.
        CreatureBox a = Box(300f, 300f);
        CreatureBox b = Box(670f, 300f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(300f), new[] { a, b }, new StageBounds(150f, MaxX, -430f, SpreadMinX: 0f));

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(new RowSlot(150f, 0f), plan.Row[0]);
        Assert.Equal(new RowSlot(480f, 0f), plan.Row[1]);
        Assert.Equal(new RowSlot(810f, 0f), plan.Row[2]);
        AssertNoOverlap(plan, Shape(300f), new[] { a, b });
    }

    [Fact]
    public void PlanClone_FloatsRatherThanOverlapping_WhenTheGroundIsFull()
    {
        // The same Toadpoles with the player standing close (SpreadMinX 75): the ground only fits them overlapping.
        CreatureBox a = Box(300f, 300f);
        CreatureBox b = Box(670f, 300f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(300f), new[] { a, b }, new StageBounds(150f, MaxX, -430f, SpreadMinX: 75f));

        Assert.Equal(ClonePlanKind.Place, plan.Kind);
        Assert.True(plan.Y < Baseline);
        AssertNoOverlap(plan, Shape(300f), new[] { a, b });
    }

    [Fact]
    public void PlanClone_LowersTheFloatClearance_BeforeAcceptingOverlap()
    {
        // Five 250px creatures fill the ground; above them only 250px of screen remains, too little for the full clearance.
        var row = new List<CreatureBox>();
        for (int i = 0; i < 5; i++)
        {
            row.Add(Box(200f + i * 170f, 250f));
        }

        var stage = new StageBounds(150f, MaxX, -250f);
        ClonePlan plan = CloneLayout.PlanClone(row[4], Shape(250f), row, stage);

        Assert.Equal(ClonePlanKind.Place, plan.Kind);
        Assert.True(plan.Y - 200f >= stage.MinTop - 0.01f, $"top {plan.Y - 200f} is off-screen");
        AssertNoOverlap(plan, Shape(250f), row);
    }

    [Fact]
    public void PlanClone_NeverOverlapsOrLeavesTheScreen_WhenThereIsRoomToFloat()
    {
        var stage = new StageBounds(150f, MaxX, -430f);
        for (int count = 1; count <= 5; count++)
        {
            for (float width = 120f; width <= 320f; width += 40f)
            {
                var row = new List<CreatureBox>();
                IReadOnlyList<RowSlot> slots = CloneLayout.Reflow(Enumerable.Repeat(width, count).ToList(), MaxX);
                foreach (RowSlot slot in slots)
                {
                    row.Add(Box(slot.CenterX, width, Baseline + slot.YOffset));
                }

                for (int original = 0; original < count; original++)
                {
                    ClonePlan plan = CloneLayout.PlanClone(row[original], Shape(width), row, stage);
                    AssertNoOverlap(plan, Shape(width), row);
                    AssertOnScreen(plan, Shape(width), row, stage);
                }
            }
        }
    }

    [Fact]
    public void PlanClone_SpreadsGiantsAcrossTheScreen_WhenNothingElseFits()
    {
        // A 640px-wide, 500px-tall giant (Byrdonis): two can't stand apart and a copy can't float above it on screen.
        var giant = new CreatureBox(640f, Baseline, 640f, -500f, 500f);
        var shape = new CloneShape(640f, -500f, 500f);
        var stage = new StageBounds(150f, MaxX, -430f);

        ClonePlan plan = CloneLayout.PlanClone(giant, shape, new[] { giant }, stage);

        // Spread from the screen's middle to its right edge: the original moves left, the clone takes the right.
        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(1, plan.CloneIndex);
        Assert.Equal(CloneLayout.SpreadMinX + 320f, plan.Row[0].CenterX, 2);
        Assert.Equal(MaxX - 320f, plan.Row[1].CenterX, 2);

        // With the player further left (SpreadMinX -300), the giants overlap by only 20px.
        ClonePlan wide = CloneLayout.PlanClone(giant, shape, new[] { giant }, stage with { SpreadMinX = -300f });
        Assert.Equal(-300f + 320f, wide.Row[0].CenterX, 2);
        Assert.Equal(MaxX - 320f, wide.Row[1].CenterX, 2);
        // The clone is raised a little (its top, -360, stays below the screen top), like vanilla's crowded rows.
        Assert.Equal(0f, plan.Row[0].YOffset);
        Assert.Equal(-CloneLayout.StaggerY, plan.Row[1].YOffset);
    }

    [Fact]
    public void SpreadRow_RaisesEverySecondCreature_WhenThereIsRoom()
    {
        // Three 400px creatures, 200px tall, don't fit on the ground; the screen top (-215) leaves only 15px above the
        // row, too little to float a copy even at the smallest clearance (20px), but enough for a 60px raise.
        CreatureBox a = Box(300f, 400f);
        CreatureBox b = Box(700f, 400f);
        var stage = new StageBounds(150f, MaxX, -215f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(400f), new[] { a, b }, stage);

        Assert.Equal(ClonePlanKind.Reflow, plan.Kind);
        Assert.Equal(0f, plan.Row[0].YOffset);
        Assert.Equal(-CloneLayout.StaggerY, plan.Row[1].YOffset);
        Assert.Equal(0f, plan.Row[2].YOffset);
        Assert.True(plan.Row[0].CenterX - 200f >= CloneLayout.SpreadMinX - 0.01f);
        Assert.True(plan.Row[2].CenterX + 200f <= MaxX + 0.01f);
    }

    [Fact]
    public void PlanClone_FloatsAtTheScreenEdge_WhenWideCreaturesLeaveNoSpotBesideThem()
    {
        // Two 500px creatures: the only floating spots on screen are at the screen edges, over one of them.
        CreatureBox a = Box(300f, 500f);
        CreatureBox b = Box(800f, 500f);

        ClonePlan plan = CloneLayout.PlanClone(a, Shape(500f), new[] { a, b }, new StageBounds(150f, MaxX, -430f));

        Assert.Equal(ClonePlanKind.Place, plan.Kind);
        Assert.Equal(400f, plan.X);
        AssertNoOverlap(plan, Shape(500f), new[] { a, b });
    }

    /// <summary>The boxes after the plan: the clone's and every creature's (moved when the row was re-laid).</summary>
    private static List<(float X, float Y, float Width)> Result(ClonePlan plan, CloneShape clone, IReadOnlyList<CreatureBox> row)
    {
        if (plan.Kind == ClonePlanKind.Place)
        {
            return row.Select(box => (box.CenterX, box.Y, box.Width)).Append((plan.X, plan.Y, clone.Width)).ToList();
        }

        var widths = row.Select(box => box.Width).ToList();
        widths.Insert(plan.CloneIndex, clone.Width);
        return plan.Row.Select((slot, i) => (slot.CenterX, Baseline + slot.YOffset, widths[i])).ToList();
    }

    private static void AssertNoOverlap(ClonePlan plan, CloneShape clone, IReadOnlyList<CreatureBox> row)
    {
        // A placed clone must clear every creature; a re-laid row must keep every pair apart.
        var boxes = Result(plan, clone, row);
        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                if (plan.Kind == ClonePlanKind.Place && j != boxes.Count - 1)
                {
                    continue;
                }

                bool apartX = MathF.Abs(boxes[i].X - boxes[j].X) >= (boxes[i].Width + boxes[j].Width) / 2f - 0.01f;
                bool apartY = MathF.Abs(boxes[i].Y - boxes[j].Y) >= 200f - 0.01f; // every test creature is 200px tall
                Assert.True(apartX || apartY, $"{plan.Kind}: boxes {i} and {j} overlap");
            }
        }
    }

    private static void AssertOnScreen(ClonePlan plan, CloneShape clone, IReadOnlyList<CreatureBox> row, StageBounds stage)
    {
        // Only what the plan moves: the clone, or the whole re-laid row.
        var boxes = Result(plan, clone, row);
        foreach ((float x, float y, float width) in plan.Kind == ClonePlanKind.Place ? boxes.Skip(boxes.Count - 1) : boxes)
        {
            Assert.True(x - width / 2f >= MathF.Min(stage.MinX / 2f, stage.SpreadMinX) - 0.01f && x + width / 2f <= stage.MaxX + 0.01f, $"{plan.Kind}: x {x} off-screen");
            Assert.True(y - 200f >= stage.MinTop - 0.01f, $"{plan.Kind}: top {y - 200f} off-screen");
        }
    }
}
