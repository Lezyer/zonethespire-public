using Xunit;
using ZoneTheSpire.Core.ForgottenEmpire;

namespace ZoneTheSpire.Tests;

public class ForgottenEmpireRulesTests
{
    [Theory]
    [InlineData(40, 12)]
    [InlineData(47, 14)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void StartingMarbled_IsThirtyPercentOfOriginalMaxHp_AtLeastOne(int maxHp, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.StartingMarbled(maxHp));
    }

    [Theory]
    [InlineData(40, 32)]
    [InlineData(47, 37)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void StatueMaxHp_IsEightyPercentOfOriginal_AtLeastOne(int maxHp, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.StatueMaxHp(maxHp));
    }

    [Theory]
    [InlineData(8, 4)]
    [InlineData(5, 2)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void MarbledCardBlock_IsHalf_RoundedDown(int block, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.MarbledCardBlock(block));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(4, 40)]
    [InlineData(0, 10)]
    public void PolishingAmount_IsTenPerPlayer(int players, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.PolishingAmount(players));
    }

    [Theory]
    [InlineData(12, false, 0.75)]
    [InlineData(1, false, 0.75)]
    [InlineData(0, false, 1.0)]
    [InlineData(12, true, 1.0)]
    public void StatueDamageMultiplier_IsQuarterLessOnlyWhileEncased(int marbled, bool unblockable, double expected)
    {
        Assert.Equal((decimal)expected, ForgottenEmpireRules.StatueDamageMultiplier(marbled, unblockable));
    }

    [Theory]
    [InlineData(10, 4, 4)]
    [InlineData(10, 25, 10)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 0, 0)]
    public void AbsorbedDamage_TakesUpToTheMarbledLeft(int marbled, int hpLoss, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.AbsorbedDamage(marbled, hpLoss));
    }

    [Theory]
    [InlineData(1, false, 5)]
    [InlineData(2, false, 10)]
    [InlineData(3, false, 15)]
    [InlineData(0, false, 5)]
    [InlineData(0, true, 5)]
    public void MarblingAmount_IsFivePerEnergy_ZeroAndXCountAsOne(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.MarblingAmount(cost, costsX));
    }

    [Theory]
    [InlineData(10, 6, 6)]
    [InlineData(4, 6, 4)]
    [InlineData(0, 6, 0)]
    public void ConvertedBlock_IsLimitedByBlockAndMarbling(int block, int marbling, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.ConvertedBlock(block, marbling));
    }

    [Theory]
    [InlineData(true, false, false, false, MarbleRewardModifier.Marbled)]
    [InlineData(false, false, false, false, MarbleRewardModifier.Marbling)]
    [InlineData(false, true, false, false, MarbleRewardModifier.None)]
    [InlineData(true, false, true, false, MarbleRewardModifier.None)]
    [InlineData(false, false, false, true, MarbleRewardModifier.None)]
    public void RewardModifier_BlockCardsAreMarbled_OthersGetMarbling(bool gainsBlock, bool unplayable, bool marbled, bool marbling, MarbleRewardModifier expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.RewardModifier(gainsBlock, unplayable, marbled, marbling));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void CanMarble_OnlyUnmarbledBlockCards(bool gainsBlock, bool marbled, bool expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.CanMarble(gainsBlock, marbled));
    }

    [Fact]
    public void CardTexts_NameTheModifiers()
    {
        Assert.Equal("[gold]Marbled[/gold].", ForgottenEmpireRules.MarbledCardText);
        Assert.Equal("[gold]Marbling[/gold] [blue]6[/blue].", ForgottenEmpireRules.MarblingCardText(6));
        Assert.Equal("Gain [green]9[/green] [gold]Marbled[/gold].", ForgottenEmpireRules.MarbledGainText("[green]9[/green]"));
    }

    [Theory]
    [InlineData(80, 40)]
    [InlineData(75, 37)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void BreakItsNose_HealsHalfOfMaxHp_RoundedDown(int maxHp, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.NoseHeal(maxHp));
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    public void EmperorsWill_TakesRemovablePlayableNonXPowers(bool isPower, bool removable, bool unplayable, bool costsX, bool expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.CanBecomeWill(isPower, removable, unplayable, costsX));
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(9, 4)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void CalcifiedCrown_KeepsHalfTheClearedBlockAsMarbled_RoundedDown(int block, int expected)
    {
        Assert.Equal(expected, ForgottenEmpireRules.CrownMarbled(block));
    }
}
