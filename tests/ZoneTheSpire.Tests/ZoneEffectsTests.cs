using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class ZoneEffectsTests
{
    private sealed class TwoEffectBiome : BiomeDefinition
    {
        public override string Id => "test_two";

        public override string DisplayName => "Test";

        public override string ColorHex => "123456";

        public override IReadOnlyList<BiomeEffect> Effects { get; } = new[]
        {
            new BiomeEffect("test.a", new[] { NodeKind.Shop, NodeKind.RestSite }, "A."),
            new BiomeEffect("test.b", new[] { NodeKind.RestSite, NodeKind.Treasure }, "B."),
        };
    }

    [Fact]
    public void Mirrorlands_HasFoesShopAndCampfireEffects()
    {
        var biome = BiomeRegistry.Get("mirrorlands")!;

        Assert.Equal(
            new[] { "mirrorlands.mirrored_foes", "mirrorlands.duplicate_service", "mirrorlands.mirrored_campfire" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(MirrorlandsBiome.MirroredFoesEffectId, biome.Effects[0].Id);
        Assert.Equal(MirrorlandsBiome.DuplicateServiceEffectId, biome.Effects[1].Id);
        Assert.Equal(MirrorlandsBiome.MirroredCampfireEffectId, biome.Effects[2].Id);

        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);

        Assert.Equal("{Zone}: one enemy is [gold]Mirrored[/gold]. [blue]1[/blue] card in each card reward has {Glam}.", biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: the merchant offers [gold]Duplicate[/gold] instead of card removal.", biome.Effects[1].TooltipLine);
        Assert.Equal("{Zone}: [gold]Rest[/gold] and [gold]Mend[/gold] also let you duplicate [blue]1[/blue] of [blue]3[/blue] random cards.", biome.Effects[2].TooltipLine);
    }

    [Fact]
    public void MultiKindEffect_MatchesEveryListedKind_AndNoOther()
    {
        var biome = BiomeRegistry.Get("mirrorlands")!;
        var affected = new HashSet<NodeKind> { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown, NodeKind.Shop, NodeKind.RestSite, NodeKind.Treasure };

        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            Assert.Equal(affected.Contains(kind) ? 1 : 0, ZoneEffects.EffectsFor(biome, kind).Count);
        }
    }

    [Fact]
    public void OverlappingEffects_AreAllReturned_InDeclarationOrder()
    {
        var biome = new TwoEffectBiome();

        Assert.Equal(new[] { "test.a", "test.b" }, ZoneEffects.EffectsFor(biome, NodeKind.RestSite).Select(e => e.Id));
        Assert.Equal(new[] { "test.a" }, ZoneEffects.EffectsFor(biome, NodeKind.Shop).Select(e => e.Id));
        Assert.Equal(new[] { "test.b" }, ZoneEffects.EffectsFor(biome, NodeKind.Treasure).Select(e => e.Id));
        Assert.Empty(ZoneEffects.EffectsFor(biome, NodeKind.Monster));
    }

    [Fact]
    public void Scrapyard_HasBotsScrapAndRummageEffects()
    {
        var biome = BiomeRegistry.Get("scrapyard")!;

        Assert.Equal(new[] { "scrapyard.scrap_bots", "scrapyard.scrap_service", "scrapyard.rummage" }, biome.Effects.Select(e => e.Id));
        Assert.Equal(ScrapyardBiome.ScrapBotsEffectId, biome.Effects[0].Id);
        Assert.Equal(ScrapyardBiome.ScrapServiceEffectId, biome.Effects[1].Id);
        Assert.Equal(ScrapyardBiome.RummageEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal("{Zone}: fights add [blue]1-2[/blue] [red]scrap bots[/red], and afterwards you can [gold]trash a card[/gold].", biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: the merchant offers [gold]Scrap[/gold] instead of card removal.", biome.Effects[1].TooltipLine);
        Assert.Equal("{Zone}: you can [gold]Rummage[/gold] through the scrap for [green]random loot[/green].", biome.Effects[2].TooltipLine);
    }

    [Fact]
    public void PrismaticStorm_HasStormBuffsTransformAndStareAtPrismEffects()
    {
        var biome = BiomeRegistry.Get("prismatic_storm")!;

        Assert.Equal(
            new[] { "prismatic_storm.storm_buffs", "prismatic_storm.transform_service", "prismatic_storm.stare_at_prism" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(PrismaticStormBiome.StormBuffsEffectId, biome.Effects[0].Id);
        Assert.Equal(PrismaticStormBiome.TransformServiceEffectId, biome.Effects[1].Id);
        Assert.Equal(PrismaticStormBiome.StareAtPrismEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(
            "{Zone}: you can [gold]Stare at Prism[/gold] for a [gold]card reward[/gold] of [green]upgraded[/green] colourless cards.",
            biome.Effects[2].TooltipLine);
        Assert.Equal(
            "{Zone}: enemies start with a random [gold]buff[/gold], and fights give [blue]2[/blue] extra [gold]card rewards[/gold] from any character.",
            biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: the merchant offers [gold]Transform[/gold] instead of card removal.", biome.Effects[1].TooltipLine);
    }

    [Fact]
    public void BloodRain_HasBloodDrinkersEffectOnFights()
    {
        var biome = BiomeRegistry.Get("blood_rain")!;
        Assert.Equal(new[] { "blood_rain.blood_drinkers", "blood_rain.blood_sacrifice", "blood_rain.blood_shop" }, biome.Effects.Select(e => e.Id));
        var shop = biome.Effects[2];
        Assert.Equal(BloodRainBiome.BloodShopEffectId, shop.Id);
        Assert.Equal(new[] { NodeKind.Shop }, shop.AffectedKinds);
        Assert.Equal("{Zone}: the merchant charges [red]HP[/red] instead of gold: [blue]1[/blue] HP per [blue]15[/blue] gold.", shop.TooltipLine);
        var effect = biome.Effects[0];
        var sacrifice = biome.Effects[1];
        Assert.Equal(BloodRainBiome.BloodSacrificeEffectId, sacrifice.Id);
        Assert.Equal(new[] { NodeKind.RestSite }, sacrifice.AffectedKinds);
        Assert.Equal("{Zone}: you can make a [gold]Blood Sacrifice[/gold]: lose [red]10[/red] Max HP to heal to full and upgrade a card.", sacrifice.TooltipLine);

        Assert.Equal(BloodRainBiome.BloodDrinkersEffectId, effect.Id);
        Assert.Equal("blood_rain.blood_drinkers", effect.Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), effect.AffectedKinds.OrderBy(k => k));
        Assert.Equal(
            "{Zone}: enemies have [gold]Blood Drinker[/gold]. Winning a fight gives [green]3[/green] Max HP ([green]5[/green] after elites).",
            effect.TooltipLine);
    }

    [Fact]
    public void HallsOfMidas_HasTouchOfMidasGildedShopAndLuxuriousRestEffects()
    {
        var biome = BiomeRegistry.Get("halls_of_midas")!;

        Assert.Equal(
            new[] { "halls_of_midas.touch_of_midas", "halls_of_midas.gilded_shop", "halls_of_midas.luxurious_rest" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(HallsOfMidasBiome.TouchOfMidasEffectId, biome.Effects[0].Id);
        Assert.Equal(HallsOfMidasBiome.GildedShopEffectId, biome.Effects[1].Id);
        Assert.Equal(HallsOfMidasBiome.LuxuriousRestEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(
            "{Zone}: enemies have [gold]Touch of Midas[/gold].",
            biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: every card for sale is [green]upgraded[/green] but costs [blue]30%[/blue] more.", biome.Effects[1].TooltipLine);
        Assert.Equal(
            "{Zone}: [gold]Rest[/gold] becomes [gold]Luxurious Rest[/gold]: it heals [green]twice as much[/green] but costs [gold]50 Gold[/gold].",
            biome.Effects[2].TooltipLine);
    }

    [Fact]
    public void PhantasmalTombs_HasPhantasmsRaidedShopAndHauntEffects()
    {
        var biome = BiomeRegistry.Get("phantasmal_tombs")!;

        Assert.Equal(
            new[] { "phantasmal_tombs.phantasms", "phantasmal_tombs.raided_shop", "phantasmal_tombs.haunt" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(PhantasmalTombsBiome.PhantasmsEffectId, biome.Effects[0].Id);
        Assert.Equal(PhantasmalTombsBiome.RaidedShopEffectId, biome.Effects[1].Id);
        Assert.Equal(PhantasmalTombsBiome.HauntEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(
            "{Zone}: enemies have [gold]Phantasm[/gold]. [blue]1[/blue] card in each card reward is [gold]Phantasm-Haunted[/gold], and rewards may include a [gold]Ghost in a Jar[/gold].",
            biome.Effects[0].TooltipLine);
        Assert.Equal(
            "{Zone}: ghosts raided the merchant: about half the stock is gone, the rest is [blue]50%[/blue] off, and its cards are [gold]Phantasm-Haunted[/gold].",
            biome.Effects[1].TooltipLine);
        Assert.Equal("{Zone}: you can [gold]Haunt[/gold]: lose [red]5[/red] HP to make [blue]3[/blue] random cards [gold]Phantasm-Haunted[/gold].", biome.Effects[2].TooltipLine);
    }

    [Fact]
    public void Ferrosand_HasMagneticFoesShopAndMagnetizeEffects()
    {
        var biome = BiomeRegistry.Get("ferrosand")!;

        Assert.Equal(
            new[] { "ferrosand.magnetic_foes", "ferrosand.magnetic_shop", "ferrosand.magnetize" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(FerrosandBiome.MagneticFoesEffectId, biome.Effects[0].Id);
        Assert.Equal(FerrosandBiome.MagneticShopEffectId, biome.Effects[1].Id);
        Assert.Equal(FerrosandBiome.MagnetizeEffectId, biome.Effects[2].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(
            "{Zone}: enemies are [gold]Magnetized[/gold] and have [gold]Ferroform[/gold]. Card rewards are [gold]Magnetic[/gold], and fights also make [blue]1[/blue] random card in your [gold]Deck[/gold] [gold]Magnetic[/gold].",
            biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: [blue]3[/blue] cards for sale are [gold]Magnetic[/gold].", biome.Effects[1].TooltipLine);
        Assert.Equal("{Zone}: you can [gold]Magnetize[/gold]: make [blue]3[/blue] random cards [gold]Magnetic[/gold].", biome.Effects[2].TooltipLine);
    }

    [Fact]
    public void Infestation_HasWrigglersEffectOnFights()
    {
        var biome = BiomeRegistry.Get("infestation")!;
        Assert.Equal(new[] { "infestation.wrigglers", "infestation.shop_wriggling", "infestation.festering_smith" }, biome.Effects.Select(e => e.Id));
        var smith = biome.Effects[2];
        Assert.Equal(InfestationBiome.FesteringSmithEffectId, smith.Id);
        Assert.Equal(new[] { NodeKind.RestSite }, smith.AffectedKinds);
        Assert.Equal("{Zone}: [gold]Smith[/gold] becomes [gold]Festering Smith[/gold]: the card also gains [gold]Wriggling[/gold].", smith.TooltipLine);
        var effect = biome.Effects[0];
        var shop = biome.Effects[1];
        Assert.Equal(InfestationBiome.ShopWrigglingEffectId, shop.Id);
        Assert.Equal(new[] { NodeKind.Shop }, shop.AffectedKinds);
        Assert.Equal("{Zone}: every card for sale has [gold]Wriggling[/gold].", shop.TooltipLine);

        Assert.Equal(InfestationBiome.WrigglersEffectId, effect.Id);
        Assert.Equal("infestation.wrigglers", effect.Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), effect.AffectedKinds.OrderBy(k => k));
        Assert.Equal(
            "{Zone}: fights start with [blue]2[/blue] [red]Infection[/red] in your [gold]Hand[/gold], and enemies that aren't minions release a [red]Wriggler[/red] when they die. Card rewards have [gold]Wriggling[/gold].",
            effect.TooltipLine);
    }

    [Theory]
    [InlineData(NodeKind.Unknown, NodeKind.Shop, NodeKind.Shop)]
    [InlineData(NodeKind.Unknown, NodeKind.Treasure, NodeKind.Treasure)]
    [InlineData(NodeKind.Unknown, NodeKind.Other, NodeKind.Unknown)]
    [InlineData(NodeKind.Unknown, NodeKind.Monster, NodeKind.Unknown)]
    [InlineData(NodeKind.Shop, NodeKind.Shop, NodeKind.Shop)]
    [InlineData(NodeKind.Monster, NodeKind.Treasure, NodeKind.Monster)]
    public void QuestionMark_CountsAsTheShopOrChestItBecame(NodeKind mapKind, NodeKind roomKind, NodeKind expected)
    {
        Assert.Equal(expected, mapKind.ResolvedBy(roomKind));
    }

    [Fact]
    public void QuestionMarkShop_GetsTheZonesShopEffects()
    {
        var biome = new PhantasmalTombsBiome();
        Assert.Equal(
            ZoneEffects.EffectsFor(biome, NodeKind.Shop).Select(effect => effect.Id),
            ZoneEffects.EffectsFor(biome, NodeKind.Unknown.ResolvedBy(NodeKind.Shop)).Select(effect => effect.Id));
    }

    [Fact]
    public void BlindingHallowed_HasFourEffects_WithTheirNodeKindsAndTooltips()
    {
        var biome = BiomeRegistry.Get("blinding_hallowed")!;

        Assert.Equal(
            new[] { "blinding_hallowed.fights", "blinding_hallowed.shop", "blinding_hallowed.repent", "blinding_hallowed.blaspheme" },
            biome.Effects.Select(e => e.Id));
        Assert.Equal(HallowedBiome.FightsEffectId, biome.Effects[0].Id);
        Assert.Equal(HallowedBiome.ShopEffectId, biome.Effects[1].Id);
        Assert.Equal(HallowedBiome.RepentEffectId, biome.Effects[2].Id);
        Assert.Equal(HallowedBiome.BlasphemeEffectId, biome.Effects[3].Id);
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[3].AffectedKinds);
        Assert.Equal(
            "{Zone}: you have [gold]Blasphemer[/gold] and enemies are [gold]Zealous[/gold]. In card rewards, attacks have [gold]Hallowing[/gold] and other cards [gold]Redemption[/gold].",
            biome.Effects[0].TooltipLine);
        Assert.Equal(
            "{Zone}: [blue]3[/blue] attacks for sale have [gold]Hallowing[/gold].",
            biome.Effects[1].TooltipLine);
        Assert.Equal(
            "{Zone}: you can [gold]Repent[/gold]: lose [red]10[/red] HP to remove all [red]Curses[/red] and give [blue]3[/blue] random attacks [gold]Hallowing[/gold].",
            biome.Effects[2].TooltipLine);
        Assert.Equal(
            "{Zone}: [gold]Smith[/gold] becomes [gold]Blaspheme[/gold]: attacks also gain [gold]Hallowing[/gold] and [gold]Blasphemous[/gold].",
            biome.Effects[3].TooltipLine);
        Assert.Equal("Blinding Hallows", biome.DisplayName);
        Assert.Equal("F2D14B", biome.ColorHex);
        // Yellow border and zone text (ColorHex), white fill on the map.
        Assert.Equal("FFFFFF", biome.MapFillColorHex);
    }

    [Fact]
    public void Fermentory_HasThreeEffects_WithTheirNodeKindsAndTooltips()
    {
        var biome = BiomeRegistry.Get("fermentory")!;

        Assert.Equal(new[] { "fermentory.fights", "fermentory.shop", "fermentory.distil" }, biome.Effects.Select(e => e.Id));
        Assert.Equal(new[] { NodeKind.Unknown, NodeKind.Monster, NodeKind.Elite }.OrderBy(k => k), biome.Effects[0].AffectedKinds.OrderBy(k => k));
        Assert.Equal(new[] { NodeKind.Shop }, biome.Effects[1].AffectedKinds);
        Assert.Equal(new[] { NodeKind.RestSite }, biome.Effects[2].AffectedKinds);
        Assert.Equal(
            "{Zone}: enemies take turns [gold]Brewing[/gold]. Winning also gives a potion slot, a potion or a [gold]Special Potion Slot[/gold].",
            biome.Effects[0].TooltipLine);
        Assert.Equal("{Zone}: [blue]6[/blue] potions for sale. A [gold]Bottle[/gold] replaces card removal.", biome.Effects[1].TooltipLine);
        Assert.Equal(
            "{Zone}: you can [gold]Distil[/gold]: gain a potion and [blue]50[/blue] [gold]Gold[/gold], and a potion slot becomes a [gold]Special Potion Slot[/gold].",
            biome.Effects[2].TooltipLine);
        Assert.Equal("The Fermentory", biome.DisplayName);
        Assert.Equal("1F4A2A", biome.ColorHex);
        Assert.Equal("3B2414", biome.MapFillColorHex);
    }

    [Fact]
    public void OtherBiomes_FillTheMapWithTheirOwnColour()
    {
        Assert.All(BiomeRegistry.All.Where(b => b.Id is not ("blinding_hallowed" or "fermentory")), b => Assert.Null(b.MapFillColorHex));
    }
}
