using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.Persistence;

namespace ZoneTheSpire.Tests;

public class ZoneStoreTests
{
    private const ulong Seed = 555UL;

    [Fact]
    public void EmptySave_GeneratesAndStoresAct()
    {
        var graph = TestMaps.Sparse(9);
        var result = ZoneStore.Ensure("", graph, Seed, 0, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Generated, result.Outcome);
        Assert.NotEmpty(result.Zones);
        Assert.Equal(ZoneDigest.Compute(0, result.Zones), result.Digest);
        Assert.Equal(result.Digest, ZoneDigest.Compute(0, ZoneStore.Read(result.Json, 0)));
    }

    [Fact]
    public void MatchingFingerprint_LoadsSavedZonesUnchanged()
    {
        var graph = TestMaps.Sparse(9);
        var first = ZoneStore.Ensure(null, graph, Seed, 0, BiomeRegistry.Ids);

        // A different seed proves the zones come from the save, not from regeneration.
        var second = ZoneStore.Ensure(first.Json, TestMaps.Shuffled(graph, 3), 1UL, 0, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Loaded, second.Outcome);
        Assert.Equal(first.Json, second.Json);
        Assert.Equal(first.Digest, second.Digest);
    }

    [Fact]
    public void ChangedMap_RegeneratesAct()
    {
        var first = ZoneStore.Ensure(null, TestMaps.Sparse(9), Seed, 0, BiomeRegistry.Ids);
        var second = ZoneStore.Ensure(first.Json, TestMaps.Sparse(10), Seed, 0, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Regenerated, second.Outcome);
        Assert.Equal(ZoneGenerator.Generate(TestMaps.Sparse(10), Seed, 0, BiomeRegistry.Ids).Count, second.Zones.Count);
    }

    [Fact]
    public void NewAct_PreservesEarlierActs()
    {
        var act0 = ZoneStore.Ensure(null, TestMaps.Sparse(9), Seed, 0, BiomeRegistry.Ids);
        var act1 = ZoneStore.Ensure(act0.Json, TestMaps.Sparse(12), Seed, 1, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Generated, act1.Outcome);
        Assert.Equal(act0.Digest, ZoneDigest.Compute(0, ZoneStore.Read(act1.Json, 0)));
        Assert.Equal(act1.Digest, ZoneDigest.Compute(1, ZoneStore.Read(act1.Json, 1)));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"Version\":99,\"Acts\":{}}")]
    public void CorruptOrUnknownVersion_RegeneratesDeterministically(string badJson)
    {
        var graph = TestMaps.Sparse(9);
        var fresh = ZoneStore.Ensure(null, graph, Seed, 0, BiomeRegistry.Ids);
        var recovered = ZoneStore.Ensure(badJson, graph, Seed, 0, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Regenerated, recovered.Outcome);
        Assert.Equal(fresh.Digest, recovered.Digest);
        Assert.Empty(ZoneStore.Read(badJson, 0));
    }

    [Fact]
    public void Serializer_RoundTripsZones()
    {
        var zone = new Zone("act1-zone3", "infestation", new[] { new GridCoord(2, 5), new GridCoord(1, 4) });
        var back = ZoneSerializer.ToZone(ZoneSerializer.FromZone(zone));

        Assert.Equal("act1-zone3", back.Id);
        Assert.Equal("infestation", back.BiomeId);
        Assert.Equal(new[] { new GridCoord(1, 4), new GridCoord(2, 5) }, back.Nodes);
    }

    [Fact]
    public void Replace_OverwritesOnlyThatAct_AndIsLoadedAfterwards()
    {
        var act0 = ZoneStore.Ensure(null, TestMaps.Sparse(9), Seed, 0, BiomeRegistry.Ids);
        var graph = TestMaps.Sparse(12);
        var act1 = ZoneStore.Ensure(act0.Json, graph, Seed, 1, BiomeRegistry.Ids);

        var whole = ZoneGenerator.WholeMap(graph, 1, "mirrorlands");
        var replaced = ZoneStore.Replace(act1.Json, graph, 1, whole);
        var reloaded = ZoneStore.Ensure(replaced.Json, graph, Seed, 1, BiomeRegistry.Ids);

        Assert.Equal(EnsureOutcome.Replaced, replaced.Outcome);
        Assert.Equal(act0.Digest, ZoneDigest.Compute(0, ZoneStore.Read(replaced.Json, 0)));
        Assert.Equal(EnsureOutcome.Loaded, reloaded.Outcome);
        Assert.Equal(ZoneDigest.Compute(1, whole), reloaded.Digest);
    }

    [Fact]
    public void Read_ReturnsEmptyForMissingAct()
    {
        var act0 = ZoneStore.Ensure(null, TestMaps.Sparse(9), Seed, 0, BiomeRegistry.Ids);
        Assert.Empty(ZoneStore.Read(act0.Json, 2));
        Assert.Empty(ZoneStore.Read(null, 0));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(555UL)]
    [InlineData(987654321UL)]
    public void ThreeActs_NeverRepeatABiome(ulong seed)
    {
        var act0 = ZoneStore.Ensure(null, TestMaps.Sparse(9), seed, 0, BiomeRegistry.Ids);
        var act1 = ZoneStore.Ensure(act0.Json, TestMaps.Sparse(12), seed, 1, BiomeRegistry.Ids);
        var act2 = ZoneStore.Ensure(act1.Json, TestMaps.Sparse(15), seed, 2, BiomeRegistry.Ids);

        var biomes = act0.Zones.Concat(act1.Zones).Concat(act2.Zones).Select(zone => zone.BiomeId).ToList();
        Assert.Equal(biomes.Count, biomes.Distinct().Count());
        Assert.NotEmpty(act2.Zones);
    }

    [Fact]
    public void BiomesForAct_ExcludesOtherActs_ButFallsBackWhenTooFewRemain()
    {
        var act0 = ZoneStore.Ensure(null, TestMaps.Sparse(9), Seed, 0, BiomeRegistry.Ids);
        var used = act0.Zones.Select(zone => zone.BiomeId).ToList();

        var forAct1 = ZoneStore.BiomesForAct(act0.Json, 1, BiomeRegistry.Ids);
        Assert.DoesNotContain(forAct1, used.Contains);
        Assert.Equal(BiomeRegistry.Ids.Count - used.Count, forAct1.Count);

        // The act's own zones don't count against it (regenerating a changed map).
        Assert.Equal(BiomeRegistry.Ids.Count, ZoneStore.BiomesForAct(act0.Json, 0, BiomeRegistry.Ids).Count);

        // Only one biome left in total: fall back to every biome rather than a single-zone act.
        var twoBiomes = new[] { used[0], "other_biome" };
        Assert.Equal(twoBiomes, ZoneStore.BiomesForAct(act0.Json, 1, twoBiomes));
    }
}
