using Xunit;
using ZoneTheSpire.Core.Midas;

namespace ZoneTheSpire.Tests;

public class HallsOfMidasRulesTests
{
    [Theory]
    [InlineData(7, 21)]
    [InlineData(1, 3)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    public void GoldForDamage_IsThreeGoldPerUnblockedDamage(int damage, int expected)
    {
        Assert.Equal(expected, HallsOfMidasRules.GoldForDamage(damage));
    }

    [Theory]
    [InlineData(50, 65)]
    [InlineData(75, 98)]
    [InlineData(150, 195)]
    [InlineData(37, 48)]
    [InlineData(0, 0)]
    public void CardPrice_IsThirtyPercentMore_RoundedToNearest(int cost, int expected)
    {
        Assert.Equal(expected, HallsOfMidasRules.CardPrice(cost));
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(250, true)]
    [InlineData(0, false)]
    public void LuxuriousRest_NeedsFiftyGold(int gold, bool expected)
    {
        Assert.Equal(expected, HallsOfMidasRules.CanAffordLuxuriousRest(gold));
    }

    [Fact]
    public void LuxuriousRest_HealsDouble()
    {
        Assert.Equal(48m, HallsOfMidasRules.LuxuriousHealAmount(24m));
        Assert.Equal(49.2m, HallsOfMidasRules.LuxuriousHealAmount(24.6m));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(250, true)]
    public void PayTribute_NeedsTheFullHundredGold(int gold, bool expected)
    {
        Assert.Equal(expected, HallsOfMidasRules.CanPayTribute(gold));
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1, 250)]
    [InlineData(2, 280)]
    [InlineData(3, 280)]
    public void PlunderGold_Is200_250_280ByAct(int actIndex, int expected)
    {
        Assert.Equal(expected, HallsOfMidasRules.PlunderGold(actIndex));
    }

    [Fact]
    public void Gilded_PlayableCardsOnce_GainFiveGoldLoseOneHp()
    {
        Assert.True(HallsOfMidasRules.CanGild(unplayable: false, alreadyGilded: false));
        Assert.False(HallsOfMidasRules.CanGild(unplayable: true, alreadyGilded: false));
        Assert.False(HallsOfMidasRules.CanGild(unplayable: false, alreadyGilded: true));
        Assert.Equal(5, HallsOfMidasRules.GildedGold);
        Assert.Equal(1, HallsOfMidasRules.GildedHpLoss);
        Assert.Equal("[gold]Gilded[/gold]: gain [gold]5 Gold[/gold], lose [red]1[/red] HP.", HallsOfMidasRules.GildedCardText);
    }
}
