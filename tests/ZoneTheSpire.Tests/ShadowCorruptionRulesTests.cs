using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Tests;

public class ShadowCorruptionRulesTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    [InlineData(5, 25)]
    public void Doom_Is5PerEnergyPaid_Or3WhenFree(int energySpent, int expected)
    {
        Assert.Equal(expected, ShadowCorruptionRules.DoomFor(energySpent));
    }

    [Fact]
    public void CardText_ShowsTheDoomForTheCurrentCost_And5XForXCost()
    {
        Assert.Equal("[purple]Shadow Corrupted[/purple]: gain [blue]10[/blue] [gold]Doom[/gold].", ShadowCorruptionRules.CardText(costsX: false, currentCost: 2));
        Assert.Equal("[purple]Shadow Corrupted[/purple]: gain [blue]3[/blue] [gold]Doom[/gold].", ShadowCorruptionRules.CardText(costsX: false, currentCost: 0));
        Assert.Equal("[purple]Shadow Corrupted[/purple]: gain [blue]5X[/blue] [gold]Doom[/gold].", ShadowCorruptionRules.CardText(costsX: true, currentCost: 0));
    }

    [Fact]
    public void RewardPick_IsDeterministic_InRange_AndVariesByReward()
    {
        Assert.Equal(-1, ShadowCorruptionRules.PickRewardIndex(1UL, "a1r2c3", 4UL, "A,B,C", 0));
        int first = ShadowCorruptionRules.PickRewardIndex(9UL, "a1r2c3", 4UL, "A,B,C", 3);
        Assert.InRange(first, 0, 2);
        Assert.Equal(first, ShadowCorruptionRules.PickRewardIndex(9UL, "a1r2c3", 4UL, "A,B,C", 3));
        Assert.Contains(Enumerable.Range(0, 30), i =>
            ShadowCorruptionRules.PickRewardIndex(9UL, "a1r2c3", 4UL, "A,B,C" + i, 3) != first);
    }

    [Theory]
    [InlineData(7, 3)]
    [InlineData(3, 3)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void ShopSlots_AreThreeDistinctSlots_IndependentFromShading(int eligible, int expected)
    {
        var slots = ShadowCorruptionRules.PickShopSlots(99UL, "a1r5c4", 3UL, eligible);
        Assert.Equal(expected, slots.Count);
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.All(slots, slot => Assert.InRange(slot, 0, eligible - 1));
        Assert.Equal(slots.OrderBy(slot => slot), slots);
        Assert.Equal(slots, ShadowCorruptionRules.PickShopSlots(99UL, "a1r5c4", 3UL, eligible));
    }

    [Fact]
    public void GildedText_ShowsTheDoubledGold_ButNotADoubledHpLoss()
    {
        Assert.Equal("[gold]Gilded[/gold]: gain [gold]10 Gold[/gold], lose [red]1[/red] HP.", HallsOfMidasRules.GildedCardTextFor(10));
        Assert.Equal(HallsOfMidasRules.GildedCardTextFor(HallsOfMidasRules.GildedGold), HallsOfMidasRules.GildedCardText);
    }
}
