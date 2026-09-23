using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class ForgottenEmpireBiomeTests
{
    [Fact]
    public void ForgottenEmpire_HasStatuesShopAndSculptEffects()
    {
        var biome = BiomeRegistry.Get("forgotten_empire")!;

        Assert.Equal("Forgotten Empire", biome.DisplayName);
        Assert.Equal(
            new[] { "forgotten_empire.statues", "forgotten_empire.marbled_shop", "forgotten_empire.sculpt" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(ForgottenEmpireBiome.StatuesEffectId, biome.Effects[0].Id);
        Assert.Equal(ForgottenEmpireBiome.MarbledShopEffectId, biome.Effects[1].Id);
        Assert.Equal(ForgottenEmpireBiome.SculptEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.All(biome.Effects, effect => Assert.StartsWith("{Zone}: ", effect.TooltipLine));
    }
}
