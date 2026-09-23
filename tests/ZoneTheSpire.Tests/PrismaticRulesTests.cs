using System.Collections.Generic;
using Xunit;
using ZoneTheSpire.Core.Prismatic;

namespace ZoneTheSpire.Tests;

public class PrismaticRulesTests
{
    [Theory]
    [InlineData(PrismaticBuff.Enrage, 0, 1)]
    [InlineData(PrismaticBuff.Enrage, 1, 1)]
    [InlineData(PrismaticBuff.Enrage, 2, 2)]
    [InlineData(PrismaticBuff.Ritual, 0, 1)]
    [InlineData(PrismaticBuff.Ritual, 1, 2)]
    [InlineData(PrismaticBuff.Ritual, 2, 3)]
    [InlineData(PrismaticBuff.Strength, 0, 3)]
    [InlineData(PrismaticBuff.Strength, 1, 4)]
    [InlineData(PrismaticBuff.Strength, 2, 5)]
    [InlineData(PrismaticBuff.Regen, 0, 4)]
    [InlineData(PrismaticBuff.Regen, 1, 5)]
    [InlineData(PrismaticBuff.Regen, 2, 7)]
    [InlineData(PrismaticBuff.Intangible, 0, 1)]
    [InlineData(PrismaticBuff.Intangible, 1, 1)]
    [InlineData(PrismaticBuff.Intangible, 2, 2)]
    [InlineData(PrismaticBuff.Thorns, 0, 3)]
    [InlineData(PrismaticBuff.Thorns, 1, 4)]
    [InlineData(PrismaticBuff.Thorns, 2, 5)]
    [InlineData(PrismaticBuff.Plating, 0, 10)]
    [InlineData(PrismaticBuff.Plating, 1, 15)]
    [InlineData(PrismaticBuff.Plating, 2, 20)]
    [InlineData(PrismaticBuff.PersonalHive, 0, 1)]
    [InlineData(PrismaticBuff.PersonalHive, 1, 1)]
    [InlineData(PrismaticBuff.PersonalHive, 2, 1)]
    [InlineData(PrismaticBuff.Slippery, 0, 2)]
    [InlineData(PrismaticBuff.Slippery, 1, 3)]
    [InlineData(PrismaticBuff.Slippery, 2, 5)]
    [InlineData(PrismaticBuff.Strength, 5, 5)]
    public void BuffAmount_FollowsTheActTable(PrismaticBuff buff, int actIndex, int expected)
    {
        Assert.Equal(expected, PrismaticRules.BuffAmount(buff, actIndex, 50));
    }

    [Theory]
    [InlineData(40, 0, 6)]
    [InlineData(41, 0, 6)]
    [InlineData(50, 1, 8)]
    [InlineData(40, 2, 8)]
    [InlineData(43, 2, 9)]
    [InlineData(3, 0, 1)]
    [InlineData(0, 2, 1)]
    public void BuffAmount_CurlUpIsAPercentageOfBaseHp(int baseMaxHp, int actIndex, int expected)
    {
        Assert.Equal(expected, PrismaticRules.BuffAmount(PrismaticBuff.CurlUp, actIndex, baseMaxHp));
    }

    [Fact]
    public void PickBuff_IsDeterministic_AndCanPickEveryBuff()
    {
        var seen = new HashSet<PrismaticBuff>();
        for (int enemy = 0; enemy < 400; enemy++)
        {
            PrismaticBuff buff = PrismaticRules.PickBuff(31UL, "a1:r4:c2:id0", enemy);
            Assert.Equal(buff, PrismaticRules.PickBuff(31UL, "a1:r4:c2:id0", enemy));
            seen.Add(buff);
        }

        Assert.Equal(PrismaticRules.AllBuffs.Count, seen.Count);
    }

    [Fact]
    public void PickBuff_VariesBetweenEnemiesAndFights()
    {
        var byEnemy = new HashSet<PrismaticBuff>();
        var byFight = new HashSet<PrismaticBuff>();
        for (int i = 0; i < 60; i++)
        {
            byEnemy.Add(PrismaticRules.PickBuff(5UL, "a0:r2:c3:id0", i));
            byFight.Add(PrismaticRules.PickBuff(5UL, $"a0:r{i}:c3:id0", 0));
        }

        Assert.True(byEnemy.Count > 5);
        Assert.True(byFight.Count > 5);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(8, false)]
    public void SnatchTheBundle_NeedsMoreThanEightHp(int currentHp, bool expected)
    {
        Assert.Equal(expected, PrismaticRules.CanSnatch(currentHp));
    }

    [Theory]
    [InlineData(0, false, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(0, true, false, false)]
    [InlineData(0, false, true, false)]
    public void TradeColours_OnlyRollsNaturallyFreeCards(int printedCost, bool costsX, bool unplayable, bool expected)
    {
        Assert.Equal(expected, PrismaticRules.IsTradeReplacement(printedCost, costsX, unplayable));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void AnotherCharacter_MeansAnotherCharactersPool(bool inCharacterPool, bool inOwnPool, bool expected)
    {
        Assert.Equal(expected, PrismaticRules.IsFromAnotherCharacter(inCharacterPool, inOwnPool));
    }

    [Fact]
    public void Bundle_ReplaysOnlyTheFirstOffColourCardEachTurn()
    {
        Assert.True(PrismaticRules.BundleReplays(0));
        Assert.False(PrismaticRules.BundleReplays(1));
        Assert.Equal(3, PrismaticRules.ColourThiefChoices);
    }
}
