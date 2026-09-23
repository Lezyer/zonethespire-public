using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class MapModelTests
{
    private static MapGraph SmallGraph(bool reversed = false, NodeKind topKind = NodeKind.Elite)
    {
        var nodes = new List<MapNode>
        {
            new(new GridCoord(0, 1), NodeKind.Monster),
            new(new GridCoord(1, 1), NodeKind.Shop),
            new(new GridCoord(0, 2), topKind),
        };
        var edges = new List<(GridCoord, GridCoord)>
        {
            (new GridCoord(0, 1), new GridCoord(0, 2)),
            (new GridCoord(1, 1), new GridCoord(0, 2)),
        };
        if (reversed)
        {
            nodes.Reverse();
            edges.Reverse();
        }

        return new MapGraph(nodes, edges);
    }

    [Fact]
    public void GridCoord_OrdersByRowThenColumn()
    {
        var sorted = new[] { new GridCoord(2, 1), new GridCoord(0, 2), new GridCoord(0, 1) }.OrderBy(c => c).ToList();
        Assert.Equal(new[] { new GridCoord(0, 1), new GridCoord(2, 1), new GridCoord(0, 2) }, sorted);
    }

    [Theory]
    [InlineData(1, 1, 2, 2, true)]
    [InlineData(1, 1, 1, 2, true)]
    [InlineData(1, 1, 2, 1, true)]
    [InlineData(1, 1, 1, 1, false)]
    [InlineData(1, 1, 3, 1, false)]
    [InlineData(1, 1, 1, 3, false)]
    public void GridCoord_Adjacency(int c1, int r1, int c2, int r2, bool expected)
    {
        Assert.Equal(expected, new GridCoord(c1, r1).IsAdjacentTo(new GridCoord(c2, r2)));
    }

    [Fact]
    public void NodeKind_OnlyOtherIsIneligible()
    {
        Assert.False(NodeKind.Other.IsZoneEligible());
        Assert.True(NodeKind.Unknown.IsZoneEligible());
        Assert.True(NodeKind.Elite.IsZoneEligible());
    }

    [Fact]
    public void MapGraph_SortsAndDeduplicates()
    {
        var graph = new MapGraph(
            new[]
            {
                new MapNode(new GridCoord(1, 2), NodeKind.Monster),
                new MapNode(new GridCoord(0, 1), NodeKind.Shop),
                new MapNode(new GridCoord(1, 2), NodeKind.Elite),
            },
            new[]
            {
                (new GridCoord(0, 1), new GridCoord(1, 2)),
                (new GridCoord(0, 1), new GridCoord(1, 2)),
            });

        Assert.Equal(new[] { new GridCoord(0, 1), new GridCoord(1, 2) }, graph.Nodes.Select(n => n.Coord));
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Fingerprint_IgnoresInputOrder()
    {
        Assert.Equal(MapFingerprint.Compute(SmallGraph()), MapFingerprint.Compute(SmallGraph(reversed: true)));
    }

    [Fact]
    public void Fingerprint_IgnoresChangesBetweenEligibleKinds()
    {
        Assert.Equal(MapFingerprint.Compute(SmallGraph(topKind: NodeKind.Elite)), MapFingerprint.Compute(SmallGraph(topKind: NodeKind.RestSite)));
    }

    [Fact]
    public void Fingerprint_ChangesWhenEligibilityOrStructureChanges()
    {
        string baseline = MapFingerprint.Compute(SmallGraph());
        Assert.NotEqual(baseline, MapFingerprint.Compute(SmallGraph(topKind: NodeKind.Other)));

        var extraEdge = new MapGraph(SmallGraph().Nodes, SmallGraph().Edges.Append((new GridCoord(0, 1), new GridCoord(1, 1))));
        Assert.NotEqual(baseline, MapFingerprint.Compute(extraEdge));
        Assert.Matches("^[0-9a-f]{16}$", baseline);
    }

    [Fact]
    public void ZoneDigest_IgnoresZoneAndNodeOrder_ButSeesBiomeChanges()
    {
        var a = new Zone("act0-zone0", "scrapyard", new[] { new GridCoord(0, 1), new GridCoord(1, 1) });
        var b = new Zone("act0-zone1", "mirrorlands", new[] { new GridCoord(0, 2) });
        var aReordered = new Zone("act0-zone0", "scrapyard", new[] { new GridCoord(1, 1), new GridCoord(0, 1) });
        var aOtherBiome = new Zone("act0-zone0", "infestation", new[] { new GridCoord(0, 1), new GridCoord(1, 1) });

        Assert.Equal(ZoneDigest.Compute(0, new[] { a, b }), ZoneDigest.Compute(0, new[] { b, aReordered }));
        Assert.NotEqual(ZoneDigest.Compute(0, new[] { a, b }), ZoneDigest.Compute(0, new[] { aOtherBiome, b }));
        Assert.NotEqual(ZoneDigest.Compute(0, new[] { a, b }), ZoneDigest.Compute(1, new[] { a, b }));
    }

    [Fact]
    public void Zone_StoresNodesDeduplicatedAndSorted()
    {
        var zone = new Zone("act0-zone0", "scrapyard", new[] { new GridCoord(1, 2), new GridCoord(0, 1), new GridCoord(1, 2) });
        Assert.Equal(new[] { new GridCoord(0, 1), new GridCoord(1, 2) }, zone.Nodes);
    }
}
