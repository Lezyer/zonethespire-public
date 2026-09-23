using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class KarmaRulesTests
{
    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(3, 1, 2)]
    [InlineData(3, 5, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(2, -1, 3)]
    [InlineData(0, -2, 2)]
    public void Cost_IsLoweredByPositiveKarmaAndRaisedByNegative(int cost, int karma, int expected)
    {
        Assert.Equal(expected, KarmaRules.CostWithKarma(cost, karma));
    }

    [Theory]
    // Karma 5 on a 3-cost card pays 3 and keeps 2; Karma 1 on a 3-cost card spends its 1.
    [InlineData(5, 3, 0, 3)]
    [InlineData(1, 3, 2, 1)]
    // Nothing is spent when the card was free anyway, or when its Karma is negative.
    [InlineData(5, 0, 0, 0)]
    [InlineData(-2, 4, 4, 0)]
    public void KarmaSpent_IsOnlyTheDiscountActuallyUsed(int karma, int withoutKarma, int withKarma, int expected)
    {
        Assert.Equal(expected, KarmaRules.KarmaSpent(karma, withoutKarma, withKarma));
    }

    [Theory]
    [InlineData(3, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, -1, 0)]
    [InlineData(-4, -4, 0)]
    public void XKarma_AddsOneWhenPositiveAndSubtractsTheWholeNegative(int karma, int expectedBonus, int expectedSpent)
    {
        Assert.Equal(expectedBonus, KarmaRules.XBonusFor(karma));
        Assert.Equal(expectedSpent, KarmaRules.XKarmaSpent(karma));
    }

    [Theory]
    // 2 energy with Karma 1 plays as X 3; with Karma -1 as X 1; more negative than X plays as X 0.
    [InlineData(2, 1, 3)]
    [InlineData(2, -1, 1)]
    [InlineData(2, -4, 0)]
    public void XValue_NeverGoesBelowZero(int x, int bonus, int expected)
    {
        Assert.Equal(expected, KarmaRules.XWithKarma(x, bonus));
    }

    [Theory]
    // A 0-cost card with Karma passes 1 on, but only when there is a card in hand to take it.
    [InlineData(2, 0, false, 3, true)]
    [InlineData(1, 0, false, 1, true)]
    [InlineData(2, 0, false, 0, false)]
    // Cards that cost something spend their Karma as a discount instead, and X-cost cards have their own rule.
    [InlineData(2, 1, false, 3, false)]
    [InlineData(2, 0, true, 3, false)]
    [InlineData(0, 0, false, 3, false)]
    [InlineData(-1, 0, false, 3, false)]
    public void ZeroCostCards_PassKarmaOnWhenThereIsSomewhereForItToGo(int karma, int cost, bool costsX, int cardsInHand, bool expected)
    {
        Assert.Equal(expected, KarmaRules.GivesKarmaAway(karma, cost, costsX, cardsInHand));
    }

    [Fact]
    public void KarmaGift_PicksACardInHand_Deterministically()
    {
        int first = KarmaRules.PickKarmaGiftIndex(9UL, 3UL, 2, 0, 5);
        Assert.Equal(first, KarmaRules.PickKarmaGiftIndex(9UL, 3UL, 2, 0, 5));
        Assert.InRange(first, 0, 4);

        // Each player, turn and gift picks on its own, and an empty hand has nothing to pick.
        Assert.NotEqual(first, KarmaRules.PickKarmaGiftIndex(9UL, 4UL, 2, 0, 5));
        Assert.Equal(-1, KarmaRules.PickKarmaGiftIndex(9UL, 3UL, 2, 0, 0));

        List<int> picks = Enumerable.Range(0, 200)
            .Select(gift => KarmaRules.PickKarmaGiftIndex(9UL, 3UL, 2, gift, 3))
            .ToList();
        Assert.Equal(new[] { 0, 1, 2 }, picks.Distinct().OrderBy(pick => pick));
    }

    [Theory]
    [InlineData(-3, -2)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    public void Recovery_GivesBackOneAndNeverGoesAboveZero(int karma, int expected)
    {
        Assert.Equal(expected, KarmaRules.Recover(karma));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    public void Meditate_GivesChakraTwoOrDoublesIt(int chakra, int expected)
    {
        Assert.Equal(expected, KarmaRules.MeditatedChakra(chakra));
    }

    [Theory]
    [InlineData(80, 12)]
    [InlineData(75, 11)]
    [InlineData(6, 1)]
    [InlineData(0, 0)]
    public void Meditate_HealsFifteenPercent(int maxHp, int expected)
    {
        Assert.Equal(expected, KarmaRules.MeditateHeal(maxHp));
    }

    [Fact]
    public void CardText_NamesTheKeywordAndTheNumber()
    {
        Assert.Equal("[gold]Karma[/gold] [blue]2[/blue]", KarmaRules.KarmaText(2));
        Assert.Equal("[gold]Karma[/gold] [blue]-1[/blue]", KarmaRules.KarmaText(-1));
        Assert.Equal("[gold]Chakra[/gold] [blue]3[/blue]", KarmaRules.ChakraText(3));
    }

    [Fact]
    public void RewardChakra_PicksOneEligibleCardAndOneToThreeChakra_Deterministically()
    {
        (int Index, int Chakra) first = KarmaRules.PickRewardChakra(42UL, "0:3:4", 7UL, "a,b,c", 3);
        Assert.Equal(first, KarmaRules.PickRewardChakra(42UL, "0:3:4", 7UL, "a,b,c", 3));
        Assert.InRange(first.Index, 0, 2);
        Assert.InRange(first.Chakra, KarmaRules.RewardChakraMin, KarmaRules.RewardChakraMax);

        // Every reward, player and node rolls on its own.
        Assert.NotEqual(first, KarmaRules.PickRewardChakra(42UL, "0:3:4", 8UL, "a,b,c", 3));
        Assert.Equal((-1, 0), KarmaRules.PickRewardChakra(42UL, "0:3:4", 7UL, "a,b,c", 0));

        List<int> chakras = Enumerable.Range(0, 300)
            .Select(i => KarmaRules.PickRewardChakra(42UL, "0:3:" + i, 7UL, "a", 1).Chakra)
            .ToList();
        Assert.Equal(new[] { 1, 2, 3 }, chakras.Distinct().OrderBy(c => c));
    }

    [Fact]
    public void Shop_SellsSixRelicsAndEightPotions_LaidOutInRows()
    {
        Assert.Equal(6, KarmaRules.ShopRelics);
        Assert.Equal(8, KarmaRules.ShopPotions);

        // The 6 relics sit in one row, evenly spaced.
        Assert.Equal((421f, 451f), DevasShopLayout.RelicSlot(0));
        Assert.Equal((421f + 5 * DevasShopLayout.SlotStep, 451f), DevasShopLayout.RelicSlot(5));

        // The 8 potions sit in two rows of 4, the second below the first.
        Assert.Equal((571f, 595f), DevasShopLayout.PotionSlot(0));
        Assert.Equal((571f + 3 * DevasShopLayout.SlotStep, 595f), DevasShopLayout.PotionSlot(3));
        Assert.Equal((571f, 739f), DevasShopLayout.PotionSlot(4));
        Assert.Equal((571f + 3 * DevasShopLayout.SlotStep, 739f), DevasShopLayout.PotionSlot(7));
    }

    [Fact]
    public void Biome_AffectsFightsShopsAndCampfires()
    {
        var biome = new DevasDomainBiome();
        Assert.Equal("devas_domain", biome.Id);
        Assert.Equal(
            new[] { DevasDomainBiome.DevasFightsEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Monster).Select(effect => effect.Id));
        Assert.Equal(
            new[] { DevasDomainBiome.DevasFightsEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Unknown).Select(effect => effect.Id));
        Assert.Equal(
            new[] { DevasDomainBiome.DevasShopEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.Shop).Select(effect => effect.Id));
        Assert.Equal(
            new[] { DevasDomainBiome.MeditateEffectId },
            ZoneEffects.EffectsFor(biome, NodeKind.RestSite).Select(effect => effect.Id));
    }
}
