using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using ZoneTheSpire.Core.Geometry;

namespace ZoneTheSpire.Tests;

public class GeometryTests
{
    // Map-like spacing: columns ~150 px apart, rows ~166 px apart (y grows downward on screen, rows go up).
    private static Vector2 At(int col, int row) => new(col * 150f, row * -166f);

    private static readonly (int A, int B)[] NoLinks = Array.Empty<(int, int)>();

    // L-shaped zone: (0,1) (0,2) (1,3) with a diagonal link, plus a non-member at (1,2) tucked in the corner.
    private static readonly Vector2[] LShape = { At(0, 1), At(0, 2), At(1, 3) };
    private static readonly (int A, int B)[] LLinks = { (0, 1), (1, 2) };
    private static readonly Vector2[] LNonMembers = { At(1, 2), At(1, 1), At(0, 3), At(2, 3), At(0, 0) };

    private static readonly Vector2[] Cluster = { At(0, 1), At(1, 1), At(1, 2), At(2, 2), At(2, 3) };
    private static readonly (int A, int B)[] ClusterLinks = { (0, 1), (1, 2), (2, 3), (3, 4) };

    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(BlobShape.Build(new Vector2[0], NoLinks));
    }

    [Fact]
    public void Build_SingleNode_IsARoundOutlineAroundIt()
    {
        var outline = BlobShape.Build(new[] { At(3, 5) }, NoLinks);

        Assert.True(outline.Count >= 24);
        Assert.True(Contains(outline, At(3, 5)));
        Assert.All(outline, p => Assert.InRange(Vector2.Distance(p, At(3, 5)), BlobShape.DefaultRadius * 0.85f, BlobShape.DefaultRadius * 1.15f));
    }

    [Fact]
    public void Build_ContainsEveryMember()
    {
        var outline = BlobShape.Build(Cluster, ClusterLinks);
        Assert.All(Cluster, c => Assert.True(Contains(outline, c), $"member {c} outside outline"));
    }

    [Fact]
    public void Build_KeepsNonMemberNodesOutside_EvenInsideTheZonesBend()
    {
        var outline = BlobShape.Build(LShape, LLinks);

        Assert.All(LShape, c => Assert.True(Contains(outline, c), $"member {c} outside outline"));
        Assert.All(LNonMembers, c => Assert.False(Contains(outline, c), $"non-member {c} inside outline"));
    }

    [Fact]
    public void Build_OutlineIsSmooth_NoSharpTurns()
    {
        foreach (var (centers, links) in new[] { (Cluster, ClusterLinks), (LShape, LLinks) })
        {
            var outline = BlobShape.Build(centers, links);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[(i + outline.Count - 1) % outline.Count];
                Vector2 b = outline[i];
                Vector2 c = outline[(i + 1) % outline.Count];
                float turn = MathF.Abs(Angle(b - a, c - b));
                Assert.True(turn < MathF.PI / 4f, $"turn of {turn * 180f / MathF.PI:0.0} degrees at vertex {i}");
            }
        }
    }

    [Fact]
    public void Build_IsStableForSameInput()
    {
        Assert.Equal(BlobShape.Build(Cluster, ClusterLinks), BlobShape.Build(Cluster, ClusterLinks));
    }

    [Fact]
    public void LabelLayout_PlacesFirstLabelCenteredOnAnchor_WhenNothingIsInTheWay()
    {
        var used = new List<LabelBox>();
        var box = LabelLayout.Place(new Vector2(100, 50), new Vector2(200, 40), used, Array.Empty<LabelBox>());

        Assert.Equal(new LabelBox(0, 30, 200, 40), box);
        Assert.Single(used);
    }

    [Fact]
    public void LabelLayout_AvoidsEarlierLabels()
    {
        var used = new List<LabelBox>();
        var first = LabelLayout.Place(new Vector2(0, 0), new Vector2(200, 36), used, Array.Empty<LabelBox>());
        var second = LabelLayout.Place(new Vector2(10, 5), new Vector2(200, 36), used, Array.Empty<LabelBox>());

        Assert.False(first.Overlaps(second));
        Assert.Equal(2, used.Count);
    }

    [Fact]
    public void LabelLayout_AvoidsNodeIconObstacles()
    {
        var icons = new[] { new LabelBox(-40, -40, 80, 80), new LabelBox(110, -40, 80, 80), new LabelBox(-190, -40, 80, 80) };
        var box = LabelLayout.Place(Vector2.Zero, new Vector2(180, 32), new List<LabelBox>(), icons);

        Assert.All(icons, icon => Assert.False(icon.Overlaps(box), $"label {box} overlaps icon {icon}"));
    }

    [Fact]
    public void LabelLayout_WhenFullyBlocked_ChoosesLeastOverlap()
    {
        // A wall of obstacles everywhere except a thin sliver of partial overlap to the right of the anchor.
        var wall = new List<LabelBox>();
        for (int x = -600; x <= 600; x += 40)
        {
            for (int y = -400; y <= 400; y += 40)
            {
                if (!(x >= 160 && x <= 200 && y == 0))
                {
                    wall.Add(new LabelBox(x, y, 40, 40));
                }
            }
        }

        var box = LabelLayout.Place(Vector2.Zero, new Vector2(120, 32), new List<LabelBox>(), wall);
        float centreOverlap = OverlapArea(new LabelBox(-60, -16, 120, 32), wall);

        Assert.True(OverlapArea(box, wall) < centreOverlap);
    }

    [Fact]
    public void LabelBox_OverlapIsExclusiveOfTouchingEdges()
    {
        Assert.True(new LabelBox(0, 0, 10, 10).Overlaps(new LabelBox(5, 5, 10, 10)));
        Assert.False(new LabelBox(0, 0, 10, 10).Overlaps(new LabelBox(10, 0, 10, 10)));
    }

    private static float OverlapArea(LabelBox box, IEnumerable<LabelBox> others) =>
        others.Sum(o =>
            MathF.Max(0f, MathF.Min(box.X + box.Width, o.X + o.Width) - MathF.Max(box.X, o.X)) *
            MathF.Max(0f, MathF.Min(box.Y + box.Height, o.Y + o.Height) - MathF.Max(box.Y, o.Y)));

    private static float Angle(Vector2 from, Vector2 to) =>
        MathF.Atan2(from.X * to.Y - from.Y * to.X, from.X * to.X + from.Y * to.Y);

    private static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
