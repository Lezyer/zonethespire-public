using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Tests;

public class StillMonkRulesTests
{
    [Fact]
    public void Numbers_MatchTheDesign()
    {
        Assert.Equal(15, StillMonkRules.SitHpLoss);
        Assert.Equal(2, StillMonkRules.SitChakra);
        Assert.Equal(3, StillMonkRules.SitColdPerCost);
        Assert.Equal(90, StillMonkRules.ShareGold);
        Assert.Equal(2, StillMonkRules.ShareCards);
        Assert.Equal(1, StillMonkRules.ShareChakra);
        Assert.Equal(5, StillMonkRules.PackMaxHpLoss);
        Assert.Equal(2, StillMonkRules.PackCards);
    }

    [Theory]
    [InlineData(16, 1, true)]
    [InlineData(15, 1, false)]
    [InlineData(20, 0, false)]
    public void Sit_NeedsMoreHpThanItCostsAndACard(int hp, int eligible, bool expected)
    {
        Assert.Equal(expected, StillMonkRules.CanSit(hp, eligible));
    }

    [Theory]
    [InlineData(90, 1, true)]
    [InlineData(89, 5, false)]
    [InlineData(300, 0, false)]
    public void Share_NeedsTheGoldAndACardToUpgrade(int gold, int upgradable, bool expected)
    {
        Assert.Equal(expected, StillMonkRules.CanShare(gold, upgradable));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void EmptyYourPack_NeedsTwoRemovableCards(int removable, bool expected)
    {
        Assert.Equal(expected, StillMonkRules.CanEmptyPack(removable));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    public void WalkOn_OnlyAppearsWhenEverythingElseIsLocked(bool sit, bool share, bool letGo, bool expected)
    {
        Assert.Equal(expected, StillMonkRules.ShowsWalkOn(sit, share, letGo));
    }

    [Theory]
    [InlineData(2, false, 6)]
    [InlineData(1, false, 3)]
    [InlineData(0, false, 3)] // like everywhere else, a 0-cost card counts as 1
    public void Sit_GivesThreeBitingColdPerEnergy_AtLeastOne(int cost, bool costsX, int expected)
    {
        Assert.Equal(expected, StillMonkRules.SitCold(cost, costsX));
    }

    [Fact]
    public void Share_PicksTwoDistinctSeededCards()
    {
        IReadOnlyList<int> picks = StillMonkRules.PickShared(5UL, "2:6:1", 3UL, 7);

        Assert.Equal(2, picks.Count);
        Assert.Equal(2, picks.Distinct().Count());
        Assert.All(picks, index => Assert.InRange(index, 0, 6));
        Assert.Equal(picks, StillMonkRules.PickShared(5UL, "2:6:1", 3UL, 7));
        Assert.Single(StillMonkRules.PickShared(5UL, "2:6:1", 3UL, 1));
    }
}
