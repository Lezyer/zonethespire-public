using Xunit;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Tests;

public class BloodRainRulesTests
{
    [Theory]
    [InlineData(10, 7)]
    [InlineData(12, 8)]
    [InlineData(13, 9)]
    [InlineData(7, 5)]
    [InlineData(5, 4)]
    [InlineData(3, 2)]
    [InlineData(1, 1)]
    public void HealAmount_IsSeventyPercentRounded_AtLeastOne(int damage, int expected)
    {
        Assert.Equal(expected, BloodRainRules.HealAmount(damage, BloodRainRules.HealPercent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void HealAmount_IsZero_WhenNoDamageGotThrough(int damage)
    {
        Assert.Equal(0, BloodRainRules.HealAmount(damage, BloodRainRules.HealPercent));
    }

    [Theory]
    [InlineData(200, 13)]
    [InlineData(150, 10)]
    [InlineData(75, 5)]
    [InlineData(68, 5)]
    [InlineData(67, 4)]
    [InlineData(5, 1)]
    [InlineData(0, 0)]
    public void HpCost_IsGoldDividedByFifteenRounded_AtLeastOneWhenPaid(int gold, int expected)
    {
        Assert.Equal(expected, BloodRainRules.HpCost(gold));
    }

    [Theory]
    [InlineData(11, 10, true)]
    [InlineData(10, 10, false)]
    [InlineData(5, 10, false)]
    [InlineData(1, 0, true)]
    public void CanAffordHp_OnlyWhenThePurchaseLeavesThePlayerAlive(int currentHp, int hpCost, bool expected)
    {
        Assert.Equal(expected, BloodRainRules.CanAffordHp(currentHp, hpCost));
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(11, true)]
    [InlineData(10, false)]
    [InlineData(1, false)]
    public void CanSacrifice_OnlyWhenTenMaxHpCanBeLostWithoutDying(int maxHp, bool expected)
    {
        Assert.Equal(expected, BloodRainRules.CanSacrifice(maxHp));
    }

    [Fact]
    public void SacrificeCost_IsTenMaxHp()
    {
        Assert.Equal(10, BloodRainRules.SacrificeMaxHpCost);
    }

    [Fact]
    public void MaxHpReward_IsThreeForNormalFights_AndFiveForElites()
    {
        Assert.Equal(3, BloodRainRules.MaxHpReward(isElite: false));
        Assert.Equal(5, BloodRainRules.MaxHpReward(isElite: true));
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(149, false)]
    public void BuyAnUmbrella_NeedsTheFull150Gold(int gold, bool expected)
    {
        Assert.Equal(expected, BloodRainRules.CanBuyUmbrella(gold));
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(10, false)]
    public void BottleYourBlood_NeedsMoreMaxHpThanItCosts(int maxHp, bool expected)
    {
        Assert.Equal(expected, BloodRainRules.CanBottle(maxHp));
    }

    [Theory]
    [InlineData(80, 60)]
    [InlineData(75, 56)]
    [InlineData(1, 1)]
    public void JarOfBlood_HealsTo75PercentOfMaxHp_RoundedDown_AtLeastOne(int maxHp, int expected)
    {
        Assert.Equal(expected, BloodRainRules.JarHeal(maxHp));
        Assert.Equal(15, BloodRainRules.ShrugHeal);
    }
}
