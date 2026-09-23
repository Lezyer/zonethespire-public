using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Tests;

public class BiomeRegistryTests
{
    [Fact]
    public void Registry_ContainsTheFourStepOneBiomesInFixedOrder()
    {
        Assert.Equal(new[] { "scrapyard", "mirrorlands", "infestation", "prismatic_storm", "blood_rain", "halls_of_midas", "phantasmal_tombs", "ferrosand", "forgotten_empire", "shadow_corruption", "devas_domain", "hoarfrost", "blinding_hallowed", "fermentory" }, BiomeRegistry.Ids);
        Assert.Equal(
            new[] { "The Scrapyard", "Mirrorlands", "Infestation", "Prismatic Storm", "Blood Rain", "Halls of Midas", "Phantasmal Tombs", "Ferrosand", "Forgotten Empire", "Shadow Corruption", "Deva's Domain", "Hoarfrost", "Blinding Hallows", "The Fermentory" },
            BiomeRegistry.All.Select(b => b.DisplayName));
    }

    [Fact]
    public void Registry_ColoursMatchSpec()
    {
        Assert.Equal(
            new[] { "8B5A2B", "8FD3F4", "2E6B2E", "F4F4F4", "7A0A1E", "D4A017", "6FD6A8", "D9BF8F", "9A62D9", "3B2352", "F291C4", "2E6F8E", "F2D14B", "1F4A2A" },
            BiomeRegistry.All.Select(b => b.ColorHex));
        Assert.All(BiomeRegistry.All, b => Assert.Matches("^[0-9A-F]{6}$", b.ColorHex));
    }

    [Fact]
    public void Get_FindsByIdOrReturnsNull()
    {
        Assert.Equal("Mirrorlands", BiomeRegistry.Get("mirrorlands")?.DisplayName);
        Assert.Null(BiomeRegistry.Get("removed_biome"));
    }
}
