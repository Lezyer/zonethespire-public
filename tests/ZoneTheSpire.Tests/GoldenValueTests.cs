using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

/// <summary>
/// These values define persisted save data: <see cref="MapFingerprint"/> output keys saved zones, and
/// <see cref="ZoneGenerator"/> output (via <see cref="ZoneDigest"/>) is what peers must agree on. If any of these
/// literals changes because production code changed, that change is a save-format break: bump
/// ZoneSerializer.CurrentVersion and plan a migration. Never just update the literals to make this pass.
/// </summary>
public class GoldenValueTests
{
    [Fact]
    public void MapFingerprint_IsPinned()
    {
        Assert.Equal("95725545f7f9f538", MapFingerprint.Compute(TestMaps.Sparse(9)));
    }

    [Fact]
    public void ZoneDigest_IsPinned()
    {
        var zones = new[]
        {
            new Zone("act0-zone0", "scrapyard", new[] { new GridCoord(0, 1), new GridCoord(1, 1) }),
            new Zone("act0-zone1", "mirrorlands", new[] { new GridCoord(0, 2) }),
        };

        Assert.Equal(0x07DF914Eu, ZoneDigest.Compute(0, zones));
    }

    [Fact]
    public void ZoneDigest_ChangesWhenOnlyZoneIdChanges()
    {
        var original = new[]
        {
            new Zone("act0-zone0", "scrapyard", new[] { new GridCoord(0, 1), new GridCoord(1, 1) }),
            new Zone("act0-zone1", "mirrorlands", new[] { new GridCoord(0, 2) }),
        };
        var changed = new[]
        {
            new Zone("act0-zone9", "scrapyard", new[] { new GridCoord(0, 1), new GridCoord(1, 1) }),
            new Zone("act0-zone1", "mirrorlands", new[] { new GridCoord(0, 2) }),
        };

        Assert.NotEqual(ZoneDigest.Compute(0, original), ZoneDigest.Compute(0, changed));
    }

    [Fact]
    public void SaveFormatVersion_IsTwo_BecauseV2GeneratorForbidsOverlap()
    {
        Assert.Equal(2, ZoneTheSpire.Core.Persistence.ZoneSerializer.CurrentVersion);
    }

    [Fact]
    public void GeneratorOutput_IsPinned()
    {
        var zones = ZoneGenerator.Generate(TestMaps.Sparse(11), 987654321UL, 0, BiomeRegistry.Ids);
        // Re-pinned when generation became 2-3 distinct-biome zones of 4-7 nodes per act starting at row 2, and again when
        // Halls of Midas, Phantasmal Tombs, Ferrosand, Forgotten Empire, Shadow Corruption, Deva's Domain, Hoarfrost, Blinding Hallows and then The Fermentory joined the biome list, and when the first act began keeping rows 1-3 free (stored zones are
        // unaffected: saved acts still load, only newly generated acts use the new rules).
        Assert.Equal(226691643u, ZoneDigest.Compute(0, zones));
    }
}
