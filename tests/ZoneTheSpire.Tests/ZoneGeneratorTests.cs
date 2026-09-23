using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class ZoneGeneratorTests
{
    private const ulong Seed = 987654321UL;

    private static IReadOnlyList<Zone> Generate(MapGraph graph, ulong seed = Seed, int act = 0) =>
        ZoneGenerator.Generate(graph, seed, act, BiomeRegistry.Ids);

    [Fact]
    public void SameInputs_ProduceIdenticalZones()
    {
        var graph = TestMaps.Sparse(11);
        Assert.Equal(ZoneDigest.Compute(0, Generate(graph)), ZoneDigest.Compute(0, Generate(graph)));
    }

    [Fact]
    public void InputEnumerationOrder_DoesNotChangeZones()
    {
        var graph = TestMaps.Sparse(11);
        for (ulong shuffle = 1; shuffle <= 5; shuffle++)
        {
            Assert.Equal(
                ZoneDigest.Compute(0, Generate(graph)),
                ZoneDigest.Compute(0, Generate(TestMaps.Shuffled(graph, shuffle))));
        }
    }

    [Fact]
    public void DifferentSeedsOrActs_ProduceDifferentZones()
    {
        var graph = TestMaps.Sparse(11);
        var bySeed = Enumerable.Range(1, 10).Select(s => ZoneDigest.Compute(0, Generate(graph, (ulong)s))).Distinct().Count();
        Assert.True(bySeed > 1);
        Assert.NotEqual(ZoneDigest.Compute(0, Generate(graph, Seed, 0)), ZoneDigest.Compute(0, Generate(graph, Seed, 1)));
    }

    [Fact]
    public void Zones_AreConnected_SizedFourToSeven_AndOnEligibleNodesOnly()
    {
        var sizes = new HashSet<int>();
        for (ulong layout = 1; layout <= 20; layout++)
        {
            var graph = TestMaps.Sparse(layout);
            var kinds = graph.Nodes.ToDictionary(n => n.Coord, n => n.Kind);
            foreach (var zone in Generate(graph, layout * 7919UL))
            {
                sizes.Add(zone.Nodes.Count);
                Assert.InRange(zone.Nodes.Count, 4, 7);
                Assert.All(zone.Nodes, c => Assert.True(kinds[c].IsZoneEligible()));
                Assert.Equal(zone.Nodes.OrderBy(c => c), zone.Nodes);
                Assert.Equal(zone.Nodes.Count, zone.Nodes.Distinct().Count());
                Assert.True(IsConnected(zone.Nodes), $"zone {zone.Id} is not connected");
            }
        }

        Assert.Equal(new[] { 4, 5, 6, 7 }, sizes.OrderBy(s => s));
    }

    [Fact]
    public void FirstRowAfterTheAncient_NeverHasAZone()
    {
        for (ulong layout = 1; layout <= 30; layout++)
        {
            foreach (var zone in Generate(TestMaps.Grid(7, 15), layout))
            {
                Assert.All(zone.Nodes, c => Assert.True(c.Row >= 2, $"zone {zone.Id} covers row {c.Row}"));
            }
        }
    }

    [Fact]
    public void FirstAct_KeepsItsFirstThreeRowsAfterTheAncientFree()
    {
        for (ulong layout = 1; layout <= 30; layout++)
        {
            foreach (var zone in Generate(TestMaps.Grid(7, 15), layout, act: 0))
            {
                Assert.All(zone.Nodes, c => Assert.True(c.Row >= 4, $"act 1 zone {zone.Id} covers row {c.Row}"));
            }
        }
    }

    [Fact]
    public void LaterActs_KeepOnlyTheFirstRowFree()
    {
        var rows = new HashSet<int>();
        for (int act = 1; act <= 2; act++)
        {
            for (ulong layout = 1; layout <= 30; layout++)
            {
                foreach (var zone in Generate(TestMaps.Grid(7, 15), layout, act))
                {
                    Assert.All(zone.Nodes, c => Assert.True(c.Row >= 2, $"act {act + 1} zone {zone.Id} covers row {c.Row}"));
                    rows.UnionWith(zone.Nodes.Select(c => c.Row));
                }
            }
        }

        // The first act's rule doesn't leak: later acts still use rows 2 and 3.
        Assert.Contains(2, rows);
        Assert.Contains(3, rows);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public void MinStartRow_IsFourInTheFirstActAndTwoAfter(int act, int expected)
    {
        Assert.Equal(expected, ZoneGenerationSettings.Default.MinStartRowFor(act));
    }

    [Fact]
    public void EachAct_HasTwoOrThreeZones_AndBothCountsHappen()
    {
        var counts = new HashSet<int>();
        for (ulong layout = 1; layout <= 30; layout++)
        {
            int count = Generate(TestMaps.Sparse(layout), layout * 104729UL).Count;
            Assert.InRange(count, 2, 3);
            counts.Add(count);
        }

        Assert.Equal(new[] { 2, 3 }, counts.OrderBy(c => c));
    }

    [Fact]
    public void Biomes_NeverRepeatWithinAnAct()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var biomes = Generate(TestMaps.Grid(7, 15), seed).Select(z => z.BiomeId).ToList();
            Assert.Equal(biomes.Count, biomes.Distinct().Count());
        }
    }

    [Fact]
    public void ZoneCount_IsCappedByDistinctBiomes()
    {
        var zones = ZoneGenerator.Generate(TestMaps.Grid(7, 15), Seed, 0, new[] { "scrapyard", "scrapyard" });
        Assert.Equal(new[] { "scrapyard" }, zones.Select(z => z.BiomeId));
    }

    [Fact]
    public void Zones_NeverShareANode_EvenWhenManyZonesAreWanted()
    {
        var greedy = new ZoneGenerationSettings { MinZonesPerAct = 5, MaxZonesPerAct = 5 };
        for (ulong layout = 1; layout <= 30; layout++)
        {
            var zones = ZoneGenerator.Generate(TestMaps.Sparse(layout), layout * 104729UL, 0, BiomeRegistry.Ids, greedy);
            var allNodes = zones.SelectMany(z => z.Nodes).ToList();
            Assert.Equal(allNodes.Count, allNodes.Distinct().Count());
        }
    }

    [Fact]
    public void AllBiomesAppear_AcrossRuns()
    {
        var used = Enumerable.Range(1, 40)
            .SelectMany(seed => Generate(TestMaps.Sparse(3), (ulong)seed))
            .Select(z => z.BiomeId)
            .Distinct()
            .OrderBy(id => id);
        Assert.Equal(BiomeRegistry.Ids.OrderBy(id => id), used);
    }

    [Fact]
    public void WholeMap_IsOneZoneOverEveryEligibleNode()
    {
        var graph = TestMaps.Sparse(4);
        var zones = ZoneGenerator.WholeMap(graph, 1, "blood_rain");

        var zone = Assert.Single(zones);
        Assert.Equal("act1-zone0", zone.Id);
        Assert.Equal("blood_rain", zone.BiomeId);
        Assert.Equal(graph.Nodes.Where(n => n.Kind.IsZoneEligible()).Select(n => n.Coord), zone.Nodes);
        Assert.Empty(ZoneGenerator.WholeMap(TestMaps.Grid(7, 15, NodeKind.Other), 0, "blood_rain"));
    }

    [Fact]
    public void ZoneIds_FollowActZoneFormat()
    {
        var zones = Generate(TestMaps.Sparse(4), act: 2);
        Assert.Equal(Enumerable.Range(0, zones.Count).Select(i => $"act2-zone{i}"), zones.Select(z => z.Id));
    }

    [Fact]
    public void DegenerateInputs_ProduceNoZones()
    {
        Assert.Empty(Generate(new MapGraph(new MapNode[0], new (GridCoord, GridCoord)[0])));
        Assert.Empty(Generate(TestMaps.Grid(7, 15, NodeKind.Other)));
        Assert.Empty(ZoneGenerator.Generate(TestMaps.Grid(7, 15), Seed, 0, new string[0]));
        Assert.Empty(Generate(TestMaps.Grid(1, 3)));
    }

    [Fact]
    public void StreamName_IsOneBasedPerAct()
    {
        Assert.Equal("zonethespire_zones_act_1", ZoneGenerator.StreamName(0));
    }

    private static bool IsConnected(IReadOnlyList<GridCoord> nodes)
    {
        var remaining = new HashSet<GridCoord>(nodes);
        var frontier = new Queue<GridCoord>();
        frontier.Enqueue(nodes[0]);
        remaining.Remove(nodes[0]);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var next in remaining.Where(current.IsAdjacentTo).ToList())
            {
                remaining.Remove(next);
                frontier.Enqueue(next);
            }
        }

        return remaining.Count == 0;
    }
}
