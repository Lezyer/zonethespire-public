using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class FrostRulesTests
{
    [Theory]
    // 3 per energy of the card's cost; a 0-cost card counts as 1, so it gets the plain rate.
    [InlineData(3, false, 9)]
    [InlineData(2, false, 6)]
    [InlineData(1, false, 3)]
    [InlineData(0, false, 3)]
    [InlineData(0, true, 3)]
    public void RewardAndShopCards_CarryThreeColdPerEnergy(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, FrostRules.RewardCold(cost, costsX));
        Assert.Equal(expected, FrostRules.ShopCold(cost, costsX));
    }

    [Theory]
    [InlineData(2, false, 8)]
    [InlineData(1, false, 4)]
    [InlineData(0, false, 4)]
    public void Frostbind_AddsFourColdPerEnergy(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, FrostRules.FrostbindCold(cost, costsX));
    }

    [Theory]
    // X-cost cards ice for their amount per energy spent; everything else ices for its amount.
    [InlineData(2, true, 3, 6)]
    [InlineData(2, true, 0, 2)]
    [InlineData(4, false, 3, 4)]
    public void XCostCards_IceForTheEnergySpent(int amount, bool costsX, int energySpent, int expected)
    {
        Assert.Equal(expected, FrostRules.PlayAmount(amount, costsX, energySpent));
    }

    [Fact]
    public void BitingCold_AddsItsWholeAmountToAHit_ThenLosesOne()
    {
        // The plan's example: a 6-damage strike into 5 Biting Cold deals 11 and leaves 4.
        Assert.Equal(5, FrostRules.DamageBonus(5));
        Assert.Equal(4, FrostRules.ColdAfterHit(5));

        // A multi-hit attack spends one per hit, so each hit adds a little less.
        var hits = new List<int>();
        int cold = 5;
        for (int hit = 0; hit < 3; hit++)
        {
            hits.Add(6 + FrostRules.DamageBonus(cold));
            cold = FrostRules.ColdAfterHit(cold);
        }

        Assert.Equal(new[] { 11, 10, 9 }, hits);
        Assert.Equal(2, cold);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void BitingCold_NeverGoesBelowZero(int cold, int expected)
    {
        Assert.Equal(expected, FrostRules.ColdAfterHit(cold));
        Assert.Equal(0, FrostRules.DamageBonus(-3));
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void TwoCardsFreezeEachTurn_OrTheWholeHandWhenItIsSmaller(int cardsInHand, int expected)
    {
        Assert.Equal(expected, FrostRules.FreezeCountFor(cardsInHand));
        Assert.Equal(2, FrostRules.FrozenPerTurn);
    }

    [Fact]
    public void FrozenCards_ArePickedDeterministically_AndDistinct()
    {
        IReadOnlyList<int> first = FrostRules.PickFrozenIndices(11UL, "0:2:3", 5UL, 1, 6, 2);
        Assert.Equal(first, FrostRules.PickFrozenIndices(11UL, "0:2:3", 5UL, 1, 6, 2));
        Assert.Equal(2, first.Count);
        Assert.Equal(first.Distinct().Count(), first.Count);
        Assert.All(first, index => Assert.InRange(index, 0, 5));

        // Every turn, player and node freezes on its own. Two draws can land on the same pair by chance, so this checks that
        // the picks move around rather than that any single pair differs.
        List<string> byTurn = Enumerable.Range(1, 30)
            .Select(turn => string.Join(",", FrostRules.PickFrozenIndices(11UL, "0:2:3", 5UL, turn, 6, 2)))
            .ToList();
        Assert.True(byTurn.Distinct().Count() > 1, "frozen cards never changed from turn to turn");

        List<string> byPlayer = Enumerable.Range(1, 30)
            .Select(player => string.Join(",", FrostRules.PickFrozenIndices(11UL, "0:2:3", (ulong)player, 1, 6, 2)))
            .ToList();
        Assert.True(byPlayer.Distinct().Count() > 1, "every player froze the same cards");
    }

    [Fact]
    public void ARandomEnemyIsPicked_Deterministically_AndNoneWhenThereAreNoEnemies()
    {
        int first = FrostRules.PickIcedEnemyIndex(3UL, "0:1:1", 2UL, "strike:4", 3);
        Assert.Equal(first, FrostRules.PickIcedEnemyIndex(3UL, "0:1:1", 2UL, "strike:4", 3));
        Assert.InRange(first, 0, 2);
        Assert.Equal(-1, FrostRules.PickIcedEnemyIndex(3UL, "0:1:1", 2UL, "strike:4", 0));

        // Separate plays ice independently.
        List<int> picks = Enumerable.Range(0, 200)
            .Select(play => FrostRules.PickIcedEnemyIndex(3UL, "0:1:1", 2UL, "strike:" + play, 3))
            .ToList();
        Assert.Equal(new[] { 0, 1, 2 }, picks.Distinct().OrderBy(pick => pick));
    }

    [Fact]
    public void CardText_NamesTheKeywordAndTheNumber()
    {
        Assert.Equal("[gold]Biting Cold[/gold] [blue]4[/blue]", FrostRules.CardText(4, costsX: false));
        Assert.Equal("[gold]Biting Cold[/gold] [blue]2[/blue]X", FrostRules.CardText(2, costsX: true));
    }

    [Fact]
    public void Biome_AffectsFightsShopsAndCampfires()
    {
        var biome = new HoarfrostBiome();
        Assert.Equal("hoarfrost", biome.Id);
        Assert.Equal(
            new[] { HoarfrostBiome.FrozenHandEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Monster).Select(effect => effect.Id));
        Assert.Equal(
            new[] { HoarfrostBiome.FrozenHandEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Unknown).Select(effect => effect.Id));
        Assert.Equal(
            new[] { HoarfrostBiome.FrostShopEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Shop).Select(effect => effect.Id));
        Assert.Equal(
            new[] { HoarfrostBiome.FrostbindEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.RestSite).Select(effect => effect.Id));
    }
}

public class FrostfallenRulesTests
{
    [Theory]
    // Half the gold, rounded down, but a single coin is still a coin; with nothing there is nothing to lose.
    [InlineData(300, 150)]
    [InlineData(87, 43)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void DitchingGear_TakesHalfYourGold_AndNeverNothingWhileYouHaveSome(int gold, int expected)
    {
        Assert.Equal(expected, FrostfallenRules.GoldLoss(gold));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(1, false)]
    public void StrugglingThrough_NeedsMoreHpThanItCosts(int currentHp, bool expected)
    {
        Assert.Equal(15, FrostfallenRules.StruggleHpLoss);
        Assert.Equal(expected, FrostfallenRules.CanStruggle(currentHp));
    }

    [Theory]
    [InlineData(10, 3, 3)]
    [InlineData(2, 3, 2)]
    [InlineData(0, 3, 0)]
    [InlineData(5, 1, 1)]
    public void FrostIsBoundToAsManyCardsAsAsked_OrEveryEligibleOne(int eligible, int wanted, int expected)
    {
        Assert.Equal(expected, FrostfallenRules.BindCountFor(eligible, wanted));
    }

    [Theory]
    // Ditching binds at Frostbind's rate, struggling at the card reward rate; 0-cost cards count as 1.
    [InlineData(2, 8)]
    [InlineData(1, 4)]
    [InlineData(0, 4)]
    public void DitchedCards_GainFourColdPerEnergy(int cost, int expected)
    {
        Assert.Equal(4, FrostfallenRules.DitchColdPerCost);
        Assert.Equal(expected, FrostfallenRules.BoundCold(FrostfallenRules.DitchColdPerCost, cost, costsX: false));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(0, 3)]
    public void StruggledCard_GainsThreeColdPerEnergy(int cost, int expected)
    {
        Assert.Equal(3, FrostfallenRules.StruggleColdPerCost);
        Assert.Equal(expected, FrostfallenRules.BoundCold(FrostfallenRules.StruggleColdPerCost, cost, costsX: false));
    }

    [Fact]
    public void TheDoomedRelic_IsStable_AndThereIsNoneWithoutLosableRelics()
    {
        int first = FrostfallenRules.PickDoomedRelicIndex(8UL, "1:4:2", 3UL, 4);
        Assert.Equal(first, FrostfallenRules.PickDoomedRelicIndex(8UL, "1:4:2", 3UL, 4));
        Assert.InRange(first, 0, 3);
        Assert.Equal(-1, FrostfallenRules.PickDoomedRelicIndex(8UL, "1:4:2", 3UL, 0));

        // Every node and player loses its own relic.
        List<int> byNode = Enumerable.Range(0, 60)
            .Select(node => FrostfallenRules.PickDoomedRelicIndex(8UL, "1:4:" + node, 3UL, 4))
            .ToList();
        Assert.True(byNode.Distinct().Count() > 1, "the same relic index was doomed at every node");
    }
}
