using Xunit;
using ZoneTheSpire.Core.Infestation;

namespace ZoneTheSpire.Tests;

public class WrigglingRulesTests
{
    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 12)]
    public void RewardAmount_IsFourPerEnergy_WithZeroCostCountingAsOne(int cost, int expected)
    {
        Assert.Equal(expected, WrigglingRules.RewardAmount(cost, costsX: false));
    }

    [Fact]
    public void RewardAmount_IsFourForXCostCards()
    {
        Assert.Equal(4, WrigglingRules.RewardAmount(-1, costsX: true));
    }

    [Theory]
    [InlineData(0, false, 2)]
    [InlineData(1, false, 2)]
    [InlineData(2, false, 4)]
    [InlineData(3, false, 6)]
    [InlineData(-1, true, 2)]
    public void ShopAmount_IsTwoPerEnergy_WithZeroAndXCostCountingAsOne(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, WrigglingRules.ShopAmount(cost, costsX));
    }

    [Theory]
    [InlineData(0, false, 2)]
    [InlineData(2, false, 4)]
    [InlineData(-1, true, 2)]
    public void SmithAmount_IsTwoPerEnergy_WithZeroAndXCostCountingAsOne(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, WrigglingRules.SmithAmount(cost, costsX));
    }

    [Theory]
    [InlineData(10, false, 3, 10)]
    [InlineData(5, true, 0, 5)]
    [InlineData(5, true, 1, 5)]
    [InlineData(5, true, 3, 15)]
    public void PlayAmount_MultipliesByEnergySpent_OnlyForXCostCards(int amount, bool costsX, int energySpent, int expected)
    {
        Assert.Equal(expected, WrigglingRules.PlayAmount(amount, costsX, energySpent));
    }

    [Fact]
    public void CardText_ShowsTheAmount_AndMarksXCostCards()
    {
        Assert.Equal("[gold]Wriggling[/gold] [blue]10[/blue]", WrigglingRules.CardText(10, costsX: false));
        Assert.Equal("[gold]Wriggling[/gold] [blue]5[/blue]X", WrigglingRules.CardText(5, costsX: true));
    }

    [Theory]
    [InlineData(20, 5)]
    [InlineData(13, 3)]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    public void OwnerDeathDamage_IsQuarterOfMaxHp_AtLeastOne(int maxHp, int expected)
    {
        Assert.Equal(expected, WrigglingRules.OwnerDeathDamage(maxHp));
    }
}
