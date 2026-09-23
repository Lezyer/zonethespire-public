using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Tests;

public class ZoneEventRulesTests
{
    private static readonly ZoneEventCandidate Shared = new("shared_event", new[] { "scrapyard", "mirrorlands" });
    private static readonly ZoneEventCandidate ScrapOnly = new("scrap_only", new[] { "scrapyard" });
    private static readonly ZoneEventCandidate MirrorOnly = new("mirror_only", new[] { "mirrorlands" });

    [Theory]
    [InlineData(NodeKind.Unknown, true)]
    [InlineData(NodeKind.Monster, false)]
    [InlineData(NodeKind.Elite, false)]
    [InlineData(NodeKind.Treasure, false)]
    [InlineData(NodeKind.RestSite, false)]
    [InlineData(NodeKind.Shop, false)]
    [InlineData(NodeKind.Other, false)]
    public void OnlyResolvedQuestionMarkEventsCanRoll(NodeKind kind, bool expected)
    {
        Assert.Equal(expected, ZoneEventRules.ResolvedEventCanRoll(kind));
    }

    [Fact]
    public void LocationKeyIncludesActAndCoordinates()
    {
        Assert.Equal("0:7:3", ZoneEventRules.LocationKey(0, 7, 3));
        Assert.NotEqual(ZoneEventRules.LocationKey(0, 7, 3), ZoneEventRules.LocationKey(1, 7, 3));
        Assert.NotEqual(ZoneEventRules.LocationKey(0, 7, 3), ZoneEventRules.LocationKey(0, 7, 4));
    }

    [Fact]
    public void SharedEventCanBelongToMultipleZones()
    {
        ZoneEventCandidate[] candidates = { Shared };
        var visited = new HashSet<string>(StringComparer.Ordinal);

        Assert.Equal("shared_event", ZoneEventRules.PickEvent(5, "0:1:1", "scrapyard", candidates, visited, forceRoll: true));
        Assert.Equal("shared_event", ZoneEventRules.PickEvent(5, "0:1:1", "mirrorlands", candidates, visited, forceRoll: true));
        Assert.Null(ZoneEventRules.PickEvent(5, "0:1:1", "ferrosand", candidates, visited, forceRoll: true));
    }

    [Fact]
    public void ZoneCanHaveMultipleEventsAndSelectionIgnoresInputOrder()
    {
        ZoneEventCandidate[] forwards = { Shared, ScrapOnly, MirrorOnly };
        ZoneEventCandidate[] backwards = forwards.Reverse().ToArray();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        string? first = ZoneEventRules.PickEvent(12345, "1:8:2", "scrapyard", forwards, visited, forceRoll: true);
        string? second = ZoneEventRules.PickEvent(12345, "1:8:2", "scrapyard", backwards, visited, forceRoll: true);

        Assert.Equal(first, second);
        Assert.Contains(first, new[] { "shared_event", "scrap_only" });
    }

    [Fact]
    public void VisitedEventCannotRepeatEvenInAnotherZoneOrAct()
    {
        ZoneEventCandidate[] candidates = { Shared, ScrapOnly };
        var visited = new HashSet<string>(StringComparer.Ordinal) { "shared_event" };

        Assert.Equal("scrap_only", ZoneEventRules.PickEvent(77, "2:4:6", "scrapyard", candidates, visited, forceRoll: true));
        Assert.Null(ZoneEventRules.PickEvent(77, "2:4:6", "mirrorlands", candidates, visited, forceRoll: true));
    }

    [Fact]
    public void AllVisitedReturnsNullWithoutRerollingASeenEvent()
    {
        ZoneEventCandidate[] candidates = { Shared, ScrapOnly };
        var visited = new HashSet<string>(StringComparer.Ordinal) { "shared_event", "scrap_only" };

        Assert.Null(ZoneEventRules.PickEvent(99, "0:2:5", "scrapyard", candidates, visited, forceRoll: true));
    }

    [Fact]
    public void ChanceRollIsStableAndApproximatelyTwentyPercent()
    {
        bool first = ZoneEventRules.RollsZoneEvent(123456789, "0:3:4");
        bool second = ZoneEventRules.RollsZoneEvent(123456789, "0:3:4");
        Assert.Equal(first, second);

        int hits = Enumerable.Range(0, 10_000)
            .Count(index => ZoneEventRules.RollsZoneEvent(123456789, "0:" + index + ":0"));
        Assert.InRange(hits, 1_850, 2_150);
    }

    [Fact]
    public void OfferChanceUsesRunWidePityProgression()
    {
        int chance = ZoneEventRules.DefaultOfferChancePercent;

        chance = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false);
        Assert.Equal(30, chance);
        chance = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false);
        Assert.Equal(40, chance);
        chance = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false);
        Assert.Equal(50, chance);
        chance = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false);
        Assert.Equal(60, chance);
        Assert.Equal(60, ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false));

        chance = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: true);
        Assert.Equal(0, chance);
        Assert.Equal(10, ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false));
    }

    [Fact]
    public void ZeroChanceNeverRollsAndSixtyPercentChanceIsStable()
    {
        Assert.All(
            Enumerable.Range(0, 1_000),
            index => Assert.False(ZoneEventRules.RollsZoneEvent(987654321, "zero:" + index, 0)));

        int hits = Enumerable.Range(0, 10_000)
            .Count(index => ZoneEventRules.RollsZoneEvent(987654321, "sixty:" + index, 60));
        Assert.InRange(hits, 5_800, 6_200);
    }

    [Fact]
    public void PickerHonorsExplicitOfferChance()
    {
        ZoneEventCandidate[] candidates = { Shared };
        var visited = new HashSet<string>(StringComparer.Ordinal);

        Assert.Null(ZoneEventRules.PickEvent(
            42,
            "0:1:2",
            "scrapyard",
            candidates,
            visited,
            offerChancePercent: 0));
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Eulogy_UpgradesTwoOrWhatIsLeft(int upgradable, int expected)
    {
        Assert.Equal(expected, FuneralRules.EulogyUpgradeCount(upgradable));
        Assert.Equal(1, FuneralRules.EulogyRemovals);
    }

    [Fact]
    public void Funeral_Numbers()
    {
        Assert.Equal(50, FuneralRules.WillGold);
        Assert.Equal(20, FuneralRules.ClimbInMaxHp);
        Assert.Equal(1, FuneralRules.ClimbInHp);
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void ScrapMagnet_LeverMagnetizesTwo_OrEveryEligibleCardWhenFewer(int eligible, int expected)
    {
        Assert.Equal(expected, ScrapMagnetRules.LeverMagnetizeCountFor(eligible));
    }

    [Fact]
    public void ScrapMagnet_ShakeIsAFairCoin_AndLockedWhenTheHpLossWouldKill()
    {
        Assert.Equal(50, Enumerable.Range(0, 100).Count(ScrapMagnetRules.ShakeFindsRelic));
        Assert.True(ScrapMagnetRules.ShakeFindsRelic(0));
        Assert.False(ScrapMagnetRules.ShakeFindsRelic(99));
        Assert.True(ScrapMagnetRules.CanShake(6));
        Assert.False(ScrapMagnetRules.CanShake(5));
        Assert.Equal(40, ScrapMagnetRules.ThrowScrapGold);
        Assert.Equal(5, ScrapMagnetRules.ShakeHpLoss);
    }

    [Theory]
    [InlineData(300, 150, 15)]
    [InlineData(99, 49, 4)]
    [InlineData(40, 20, 2)]
    [InlineData(20, 10, 1)]
    public void Tithe_GoldPaysHalf_For1MaxHpPer10Paid(int gold, int paid, int maxHp)
    {
        Assert.Equal(paid, TitheRules.GoldTithe(gold));
        Assert.Equal(maxHp, TitheRules.MaxHpForGold(TitheRules.GoldTithe(gold)));
        Assert.True(TitheRules.CanTitheGold(gold));
        Assert.False(TitheRules.CanTitheGold(19));
    }

    [Fact]
    public void Tithe_BloodRobAndLedgerLimits()
    {
        Assert.True(TitheRules.CanTitheBlood(13));
        Assert.False(TitheRules.CanTitheBlood(12));
        Assert.Equal(2, TitheRules.GildCountFor(5));
        Assert.Equal(1, TitheRules.GildCountFor(1));
        Assert.True(TitheRules.CanSignLedger(6));
        Assert.False(TitheRules.CanSignLedger(5));
        Assert.True(TitheRules.CanRob(11));
        Assert.False(TitheRules.CanRob(10));
        Assert.Equal(50, Enumerable.Range(0, 100).Count(TitheRules.RobSucceeds));
        Assert.Equal(150, TitheRules.RobGold);
    }

    [Theory]
    [InlineData(100, 150, 50, true)]
    [InlineData(200, 150, 50, false)]
    [InlineData(100, 150, 10, false)]
    [InlineData(100, 150, 11, true)]
    [InlineData(0, 0, 50, false)]
    public void BloodLedger_PaysInHp_OnlyWhatGoldCantCover_AndNeverLethally(int gold, int price, int hp, bool paysInHp)
    {
        // 150 gold costs 10 HP (1 per 15): it needs more than 10 HP to survive.
        Assert.Equal(paysInHp, TitheRules.LedgerPaysWithHp(gold, price, hp));
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Eclipse_LightAndDarkAffectTwoCards_OrFewer(int eligible, int expected)
    {
        Assert.Equal(expected, EclipseRules.LightCountFor(eligible));
        Assert.Equal(expected, EclipseRules.DarkCountFor(eligible));
    }

    [Theory]
    // Only positive Chakra pays, at 50 gold a point; negative Chakra is ignored rather than charged for.
    [InlineData(new[] { 2, 3 }, 250)]
    [InlineData(new[] { 2, -3 }, 100)]
    [InlineData(new[] { -2, -1 }, 0)]
    [InlineData(new int[0], 0)]
    public void Pilgrim_SellsChakraForFiftyGoldAPositivePoint(int[] chakra, int expected)
    {
        Assert.Equal(expected, PilgrimRules.GoldFor(chakra));
        Assert.Equal(50, PilgrimRules.GoldPerChakra);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Pilgrim_GivesChakraToTwoCards_OrEveryEligibleCardWhenFewer(int eligible, int expected)
    {
        Assert.Equal(expected, PilgrimRules.GiftCountFor(eligible));
        Assert.Equal(2, PilgrimRules.ChakraGiftAmount);
    }

    [Fact]
    public void Pilgrim_PrefersCardsThatCostSomething_AndFallsBackToFreeOnes()
    {
        Assert.True(PilgrimRules.DrawsFromCostingCards(1));
        Assert.False(PilgrimRules.DrawsFromCostingCards(0));
    }

    [Theory]
    [InlineData(80, 8)]
    [InlineData(75, 7)]
    [InlineData(6, 1)]
    [InlineData(0, 0)]
    public void WorldlyAttachment_HealsTenPercentOnSurvival(int maxHp, int expected)
    {
        Assert.Equal(expected, PilgrimRules.AttachmentHeal(maxHp));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void WorldlyAttachment_CountsDownTwoTurnsAndStopsAtZero(int turns, int expected)
    {
        Assert.Equal(2, PilgrimRules.AttachmentTurns);
        Assert.Equal(expected, PilgrimRules.TurnsLeft(turns));
    }

    [Fact]
    public void Eclipse_SplitPrefersUpgradableCards_AndLookIsAFairCoin()
    {
        Assert.True(EclipseRules.SplitPrefersUpgradable(3));
        Assert.False(EclipseRules.SplitPrefersUpgradable(0));
        Assert.Equal(50, Enumerable.Range(0, 100).Count(EclipseRules.LookSucceeds));
        Assert.True(EclipseRules.CanLook(11));
        Assert.False(EclipseRules.CanLook(10));
    }
}
