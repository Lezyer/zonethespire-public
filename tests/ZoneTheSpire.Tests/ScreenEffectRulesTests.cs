using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Visuals;

namespace ZoneTheSpire.Tests;

public class ScreenEffectRulesTests
{
    [Theory]
    [InlineData("blood_rain", ZoneScreenEffect.BloodRain)]
    [InlineData("prismatic_storm", ZoneScreenEffect.PrismaticLight)]
    [InlineData("scrapyard", ZoneScreenEffect.DustClouds)]
    [InlineData("mirrorlands", ZoneScreenEffect.MirrorShine)]
    [InlineData("halls_of_midas", ZoneScreenEffect.GoldenRays)]
    [InlineData("infestation", ZoneScreenEffect.BuzzingFlies)]
    [InlineData("phantasmal_tombs", ZoneScreenEffect.GhostlyForms)]
    [InlineData("ferrosand", ZoneScreenEffect.Sandstorm)]
    [InlineData("forgotten_empire", ZoneScreenEffect.CrumblingRuins)]
    [InlineData("shadow_corruption", ZoneScreenEffect.CreepingShadow)]
    [InlineData("devas_domain", ZoneScreenEffect.GoldenMandalas)]
    [InlineData("hoarfrost", ZoneScreenEffect.DriftingFrost)]
    [InlineData("blinding_hallowed", ZoneScreenEffect.BlindingRadiance)]
    [InlineData("fermentory", ZoneScreenEffect.FermentingGas)]
    [InlineData("removed_biome", ZoneScreenEffect.None)]
    [InlineData(null, ZoneScreenEffect.None)]
    public void EachBiome_MapsToItsScreenEffect(string? biomeId, ZoneScreenEffect expected)
    {
        Assert.Equal(expected, ScreenEffectRules.For(biomeId));
    }

    [Fact]
    public void EffectIds_MatchRegisteredBiomes()
    {
        Assert.NotNull(BiomeRegistry.Get("blood_rain"));
        Assert.NotNull(BiomeRegistry.Get("prismatic_storm"));
        Assert.NotNull(BiomeRegistry.Get("halls_of_midas"));
        Assert.NotNull(BiomeRegistry.Get("mirrorlands"));
        Assert.NotNull(BiomeRegistry.Get("scrapyard"));
    }
}
