using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Tests;

public class ZoneRelicRulesTests
{
    private static readonly IReadOnlyDictionary<string, string> NoneAppeared = new Dictionary<string, string>();

    private static IEnumerable<string> Locations() =>
        Enumerable.Range(0, 400).Select(i => $"a0.r{i}.c{i % 7}.i0");

    [Fact]
    public void RollsOffer_IsAboutHalfTheTime_AndStableForALocation()
    {
        int offers = Locations().Count(location => ZoneRelicRules.RollsOffer(12345UL, location));
        Assert.InRange(offers, 160, 240);
        Assert.All(Locations(), location => Assert.Equal(ZoneRelicRules.RollsOffer(99UL, location), ZoneRelicRules.RollsOffer(99UL, location)));
    }

    [Fact]
    public void PickOffer_WithoutTheRoll_OffersNothing()
    {
        string location = Locations().First(l => !ZoneRelicRules.RollsOffer(7UL, l));
        Assert.Null(ZoneRelicRules.PickOffer(7UL, location, new[] { "a", "b" }, NoneAppeared));
        Assert.NotNull(ZoneRelicRules.PickOffer(7UL, location, new[] { "a", "b" }, NoneAppeared, forceOffer: true));
    }

    [Fact]
    public void PickOffer_NoCandidates_OffersNothing()
    {
        Assert.Null(ZoneRelicRules.PickOffer(7UL, "a0.r1.c1.i0", new string[0], NoneAppeared, forceOffer: true));
    }

    [Fact]
    public void PickOffer_SkipsRelicsSeenElsewhere_AndOffersTheOtherOne()
    {
        var appeared = new Dictionary<string, string> { ["a"] = "a0.r2.c3.i0" };
        Assert.All(
            Locations().Take(50),
            location => Assert.Equal("b", ZoneRelicRules.PickOffer(3UL, location, new[] { "a", "b" }, appeared, forceOffer: true)));
    }

    [Fact]
    public void PickOffer_AllSeenElsewhere_OffersNothing()
    {
        var appeared = new Dictionary<string, string> { ["a"] = "a0.r2.c3.i0", ["b"] = "a1.r4.c1.i0" };
        Assert.Null(ZoneRelicRules.PickOffer(3UL, "a0.r5.c5.i0", new[] { "a", "b" }, appeared, forceOffer: true));
    }

    [Fact]
    public void PickOffer_SameChestAgain_OffersTheSameRelic()
    {
        const string location = "a0.r5.c5.i0";
        string? first = ZoneRelicRules.PickOffer(3UL, location, new[] { "a", "b", "c" }, NoneAppeared, forceOffer: true);
        Assert.NotNull(first);

        IReadOnlyDictionary<string, string> appeared = ZoneRelicRules.ParseAppeared(ZoneRelicRules.MarkAppeared("", first!, location));
        Assert.Equal(first, ZoneRelicRules.PickOffer(3UL, location, new[] { "a", "b", "c" }, appeared, forceOffer: true));
    }

    [Fact]
    public void PickOffer_DoesNotDependOnCandidateOrder()
    {
        Assert.All(
            Locations().Take(50),
            location => Assert.Equal(
                ZoneRelicRules.PickOffer(11UL, location, new[] { "a", "b", "c" }, NoneAppeared, forceOffer: true),
                ZoneRelicRules.PickOffer(11UL, location, new[] { "c", "a", "b" }, NoneAppeared, forceOffer: true)));
    }

    [Theory]
    [InlineData(NodeKind.Treasure, true)]
    [InlineData(NodeKind.Unknown, true)]
    [InlineData(NodeKind.Monster, false)]
    [InlineData(NodeKind.Shop, false)]
    [InlineData(NodeKind.RestSite, false)]
    [InlineData(NodeKind.Other, false)]
    public void ResolvedTreasure_OffersFromTreasureAndQuestionMarkNodes(NodeKind originalMapKind, bool expected)
    {
        Assert.Equal(expected, ZoneRelicRules.ResolvedTreasureCanOffer(originalMapKind));
    }

    [Fact]
    public void Appeared_RoundTrips_AndKeepsTheFirstLocation()
    {
        string saved = ZoneRelicRules.MarkAppeared("", "b", "a0.r1.c2.i0");
        saved = ZoneRelicRules.MarkAppeared(saved, "a", "a1.r3.c4.i0");
        saved = ZoneRelicRules.MarkAppeared(saved, "b", "a2.r0.c0.i0");

        Assert.Equal("a@a1.r3.c4.i0;b@a0.r1.c2.i0", saved);
        IReadOnlyDictionary<string, string> appeared = ZoneRelicRules.ParseAppeared(saved);
        Assert.Equal("a0.r1.c2.i0", appeared["b"]);
        Assert.Equal("a1.r3.c4.i0", appeared["a"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(";;@;nolocation@;@nokey")]
    public void ParseAppeared_IgnoresEmptyAndMalformedPairs(string? saved)
    {
        Assert.Empty(ZoneRelicRules.ParseAppeared(saved));
    }

    [Fact]
    public void Catalog_InfestationOffersVerminSymbiont()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.VerminSymbiontKey }, ZoneRelicCatalog.KeysFor(new InfestationBiome().Id));
    }

    [Fact]
    public void Catalog_BloodRainOffersCrimsonTooth()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.CrimsonToothKey }, ZoneRelicCatalog.KeysFor(new BloodRainBiome().Id));
    }

    [Fact]
    public void Catalog_FerrosandOffersLargeFieldstone()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.LargeFieldstoneKey }, ZoneRelicCatalog.KeysFor(new FerrosandBiome().Id));
    }

    [Fact]
    public void Catalog_ForgottenEmpireOffersMarblePauldrons()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.MarblePauldronsKey }, ZoneRelicCatalog.KeysFor(new ForgottenEmpireBiome().Id));
    }

    [Fact]
    public void Catalog_MirrorlandsOffersReflectiveShard()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.ReflectiveShardKey }, ZoneRelicCatalog.KeysFor(new MirrorlandsBiome().Id));
    }

    [Fact]
    public void Catalog_HallsOfMidasOffersGoldenWishmaker()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.GoldenWishmakerKey }, ZoneRelicCatalog.KeysFor(new HallsOfMidasBiome().Id));
    }

    [Fact]
    public void Catalog_PhantasmalTombsOffersFlutteringPhantasm()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.FlutteringPhantasmKey }, ZoneRelicCatalog.KeysFor(new PhantasmalTombsBiome().Id));
    }

    [Fact]
    public void Catalog_ScrapyardOffersSalvageMachine()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.SalvageMachineKey }, ZoneRelicCatalog.KeysFor(new ScrapyardBiome().Id));
    }

    [Fact]
    public void Catalog_ShadowCorruptionOffersSeedOfHatred()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.SeedOfHatredKey }, ZoneRelicCatalog.KeysFor(new ShadowCorruptionBiome().Id));
    }

    [Fact]
    public void Catalog_DevasDomainOffersScrollOfChants()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.ScrollOfChantsKey }, ZoneRelicCatalog.KeysFor(new DevasDomainBiome().Id));
    }

    [Fact]
    public void Catalog_HoarfrostOffersFrostheart()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.FrostheartKey }, ZoneRelicCatalog.KeysFor(new HoarfrostBiome().Id));
    }

    [Fact]
    public void Catalog_PrismaticStormOffersPrismCloud()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.PrismCloudKey }, ZoneRelicCatalog.KeysFor(new PrismaticStormBiome().Id));
    }

    [Fact]
    public void NewZoneRelicNumbers_AreStable()
    {
        Assert.Equal(3, ZoneRelicEffects.FlutteringPhantasmHeal);
        Assert.Equal(2, ZoneRelicEffects.PrismCloudCardCount);
        Assert.Equal(3, ZoneRelicEffects.ScrollOfChantsCards);
        Assert.Equal(2, ZoneRelicEffects.ScrollOfChantsKarma);
        Assert.Equal(5, ZoneRelicEffects.FrostheartEnemyCold);
        Assert.Equal(3, ZoneRelicEffects.FrostheartSelfCold);
    }

    [Theory]
    [InlineData(0, 20, 1)]
    [InlineData(0, 19, 0)]
    [InlineData(19, 1, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(10, -5, 0)]
    public void CrimsonToothHeal_IsFivePercentOfTheRunningTotal_RoundedDown(int before, int dealt, int expected)
    {
        Assert.Equal(expected, ZoneRelicEffects.CrimsonToothHeal(before, dealt));
    }

    [Fact]
    public void CrimsonToothHeal_SmallHitsAddUpToTheSameAsOneBigHit()
    {
        int healed = 0;
        for (int i = 0; i < 20; i++)
        {
            healed += ZoneRelicEffects.CrimsonToothHeal(i, 1);
        }

        Assert.Equal(ZoneRelicEffects.CrimsonToothHeal(0, 20), healed);
    }

    [Fact]
    public void MarblePauldrons_GrantsTenPolishing()
    {
        Assert.Equal(5, ZoneRelicEffects.MarblePauldronsPolishing);
    }

    [Theory]
    [InlineData(false, 2, 30, false, true, true)]
    [InlineData(false, 30, 2, false, true, false)]
    [InlineData(true, 2, 30, false, true, false)]
    [InlineData(false, 2, 30, true, true, false)]
    [InlineData(false, 2, 30, false, false, false)]
    [InlineData(false, 0, 30, false, true, false)]
    public void ReflectiveShard_OnlyTriggersOnFirstEnemyBlockBreak(
        bool used,
        int block,
        int damage,
        bool unblockable,
        bool enemy,
        bool expected)
    {
        Assert.Equal(expected, ZoneRelicEffects.ReflectiveShardTriggers(used, block, damage, unblockable, enemy));
    }

    [Theory]
    [InlineData(10, 30, 105, 9, 105)]
    [InlineData(10, 30, 104, 30, 0)]
    [InlineData(10, 10, 5, 9, 5)]
    [InlineData(1, 1, 5, 0, 5)]
    [InlineData(10, 9, 999, 9, 0)]
    [InlineData(0, 10, 999, 10, 0)]
    public void GoldenWishmaker_RequiresFullPaymentAndLeavesOneHp(
        int hp,
        int damage,
        int gold,
        int expectedHpLoss,
        int expectedGoldCost)
    {
        GoldenWishmakerResult result = ZoneRelicEffects.GoldenWishmaker(hp, damage, gold);
        Assert.Equal(expectedHpLoss, result.HpLoss);
        Assert.Equal(expectedGoldCost, result.GoldCost);
    }

    [Fact]
    public void ChestTooltip_OnlyZonesWithRelics_WithoutNamingThem()
    {
        Assert.Equal(
            "[color=#2E6B2E]Infestation[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(new InfestationBiome(), NodeKind.Treasure, "2E6B2E", _ => null));
        Assert.Equal(
            "[color=#D9BF8F]Ferrosand[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(new FerrosandBiome(), NodeKind.Treasure, "D9BF8F", _ => null));
        Assert.Equal(
            "[color=#FFFFFF]Forgotten Empire[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(new ForgottenEmpireBiome(), NodeKind.Treasure, "FFFFFF", _ => null));
        Assert.Equal(
            "[color=#FFFFFF]Mirrorlands[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(new MirrorlandsBiome(), NodeKind.Treasure, "FFFFFF", _ => null));
        Assert.Equal(
            "[color=#FFFFFF]Halls of Midas[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(new HallsOfMidasBiome(), NodeKind.Treasure, "FFFFFF", _ => null));
        Assert.DoesNotContain(ZoneRelicCatalog.ChestRelicEffect, ZoneEffects.EffectsFor(new InfestationBiome(), NodeKind.Shop));
    }

    [Fact]
    public void Catalog_KeysAreUniqueAndSaveSafe_AndBiomesExist()
    {
        Assert.Equal(ZoneRelicCatalog.All.Count, ZoneRelicCatalog.All.Select(r => r.Key).Distinct().Count());
        Assert.All(ZoneRelicCatalog.All, relic =>
        {
            Assert.DoesNotContain('@', relic.Key);
            Assert.DoesNotContain(';', relic.Key);
            Assert.NotEmpty(relic.BiomeIds);
            Assert.All(relic.BiomeIds, id => Assert.Contains(id, BiomeRegistry.Ids));
        });
    }
}
