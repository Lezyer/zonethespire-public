using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Phantasmal;

namespace ZoneTheSpire.Tests;

public class PhantasmalRulesTests
{
    [Theory]
    [InlineData(4, 3)]
    [InlineData(7, 5)]
    [InlineData(10, 7)]
    [InlineData(100, 75)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void ReducedHpLoss_IsTwentyFivePercentLess_RoundedDown_AtLeastOne(int amount, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.ReducedHpLoss(amount));
    }

    [Theory]
    [InlineData(0, 1, 3)]
    [InlineData(1, 1, 5)]
    [InlineData(2, 1, 7)]
    [InlineData(3, 1, 7)]
    [InlineData(0, 3, 9)]
    [InlineData(1, 2, 10)]
    [InlineData(2, 4, 28)]
    [InlineData(0, 0, 3)]
    public void CopyHp_Is3_5_7ByAct_TimesPlayers(int actIndex, int playerCount, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.CopyHp(actIndex, playerCount));
    }

    [Fact]
    public void Copies_AreOnlyLeftByNonMinionPhantasmsWhoseBodyLeaves()
    {
        Assert.True(PhantasmalRules.ShouldSpawnCopy(isEnemyMonster: true, hasPhantasm: true, isMinion: false, isCopy: false, keepsBody: false));
        Assert.False(PhantasmalRules.ShouldSpawnCopy(false, true, false, false, false));
        Assert.False(PhantasmalRules.ShouldSpawnCopy(true, false, false, false, false));
        Assert.False(PhantasmalRules.ShouldSpawnCopy(true, true, true, false, false));
        Assert.False(PhantasmalRules.ShouldSpawnCopy(true, true, false, true, false));
        Assert.False(PhantasmalRules.ShouldSpawnCopy(true, true, false, false, true));
    }

    [Fact]
    public void RevivingPhantasms_RiseOnlyOnce()
    {
        Assert.True(PhantasmalRules.ShouldReviveOnce(isEnemyMonster: true, hasPhantasm: true, isCopy: false, keepsBody: true, alreadyRevived: false));
        Assert.False(PhantasmalRules.ShouldReviveOnce(true, true, false, true, true));
        Assert.False(PhantasmalRules.ShouldReviveOnce(true, true, false, false, false));
        Assert.False(PhantasmalRules.ShouldReviveOnce(true, false, false, true, false));
        Assert.False(PhantasmalRules.ShouldReviveOnce(true, true, true, true, false));
    }

    [Theory]
    [InlineData(true, false, 0.75)]
    [InlineData(false, false, 1.0)]
    [InlineData(true, true, 1.0)]
    public void DealtDamageMultiplier_IsQuarterLessForAttacksUntilRisen(bool poweredAttack, bool hasRisen, double expected)
    {
        Assert.Equal((decimal)expected, PhantasmalRules.DealtDamageMultiplier(poweredAttack, hasRisen));
    }

    [Theory]
    [InlineData(50, 10)]
    [InlineData(46, 9)]
    [InlineData(43, 9)]
    [InlineData(3, 1)]
    public void ReviveHp_IsTwentyPercentRounded_AtLeastOne(int maxHp, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.ReviveHp(maxHp));
    }

    [Theory]
    [InlineData(false, 1, false, false, true)]
    [InlineData(false, 0, false, false, true)]
    [InlineData(true, 0, false, false, true)]
    [InlineData(true, 0, true, false, false)]
    [InlineData(false, -1, false, false, false)]
    [InlineData(false, 2, true, false, false)]
    [InlineData(false, 2, false, true, false)]
    public void CanHaunt_PlayableCardsWithoutHaunting(bool costsX, int baseCost, bool unplayable, bool alreadyHaunted, bool expected)
    {
        Assert.Equal(expected, PhantasmalRules.CanHaunt(costsX, baseCost, unplayable, alreadyHaunted));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void HauntedCost_IsOneLess_NeverNegative(int cost, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.HauntedCost(cost));
    }

    [Fact]
    public void HauntedPlay_ExhaustsOnARoll_NeverCostsHp()
    {
        Assert.Equal(30, PhantasmalRules.HauntedExhaustChancePercent);
    }

    [Fact]
    public void HauntedCardText_SaysExactlyWhatPlayingDoes()
    {
        Assert.Equal("[gold]Phantasm-Haunted[/gold]: [gold]Ethereal[/gold]. [blue]30%[/blue] [gold]Exhaust[/gold].", PhantasmalRules.HauntedCardText);
        Assert.Equal("[gold]Phantasm-Haunted[/gold]: X+[blue]1[/blue]. [gold]Ethereal[/gold]. [blue]30%[/blue] [gold]Exhaust[/gold].", PhantasmalRules.HauntedXCardText);
    }

    [Theory]
    [InlineData(6, 1, true)]
    [InlineData(5, 3, false)]
    [InlineData(40, 0, false)]
    public void Haunt_NeedsMoreHpThanItCosts_AndAHauntableCard(int currentHp, int eligible, bool expected)
    {
        Assert.Equal(expected, PhantasmalRules.CanPerformHaunt(currentHp, eligible));
    }

    [Theory]
    [InlineData(10, 3)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void HauntSelectCount_IsThreeOrWhatIsLeft(int eligible, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.HauntSelectCount(eligible));
    }

    [Fact]
    public void PickHauntIndices_IsDeterministic_AndPicksDistinctAscendingInBoundIndices()
    {
        var pick = PhantasmalRules.PickHauntIndices(42UL, "a0r4c3", 1UL, 10, 3);
        Assert.Equal(pick, PhantasmalRules.PickHauntIndices(42UL, "a0r4c3", 1UL, 10, 3));
        Assert.Equal(3, pick.Count);
        Assert.Equal(pick, pick.Distinct().OrderBy(index => index));
        Assert.All(pick, index => Assert.InRange(index, 0, 9));

        Assert.Equal(2, PhantasmalRules.PickHauntIndices(42UL, "a0r4c3", 1UL, 2, 3).Count);
        Assert.Empty(PhantasmalRules.PickHauntIndices(42UL, "a0r4c3", 1UL, 0, 3));
    }

    [Fact]
    public void PickRewardHauntIndex_IsDeterministic_AndWithinEligibleCards()
    {
        int index = PhantasmalRules.PickRewardHauntIndex(42UL, "a0r4c3", 1UL, "a,b,c", 3);
        Assert.InRange(index, 0, 2);
        Assert.Equal(index, PhantasmalRules.PickRewardHauntIndex(42UL, "a0r4c3", 1UL, "a,b,c", 3));
        Assert.Equal(-1, PhantasmalRules.PickRewardHauntIndex(42UL, "a0r4c3", 1UL, "a,b,c", 0));
    }

    [Theory]
    [InlineData(150, 75)]
    [InlineData(75, 38)]
    [InlineData(51, 26)]
    [InlineData(0, 0)]
    public void DiscountedPrice_IsHalfRoundedToNearest(int cost, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.DiscountedPrice(cost));
    }

    [Fact]
    public void IsRaided_IsDeterministic_AndAboutHalfTheTime()
    {
        int raided = Enumerable.Range(0, 2000).Count(slot => PhantasmalRules.IsRaided(42UL, "a0r4c3", 1UL, "relic", slot));
        Assert.InRange(raided, 900, 1100);
        for (int slot = 0; slot < 20; slot++)
        {
            Assert.Equal(PhantasmalRules.IsRaided(42UL, "a0r4c3", 1UL, "card", slot), PhantasmalRules.IsRaided(42UL, "a0r4c3", 1UL, "card", slot));
        }

        bool anyDifferent = Enumerable.Range(0, 20).Any(slot =>
            PhantasmalRules.IsRaided(42UL, "a0r4c3", 1UL, "card", slot) != PhantasmalRules.IsRaided(42UL, "a0r4c3", 2UL, "card", slot));
        Assert.True(anyDifferent);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(75, 22)]
    [InlineData(3, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void ProcessionMaxHpLoss_IsThirtyPercentRoundedDown_AtLeastOne_NeverTheLastPoint(int maxHp, int expected)
    {
        Assert.Equal(expected, PhantasmalRules.ProcessionMaxHpLoss(maxHp));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [InlineData(1, false)]
    public void BottleTheSpirits_NeedsMoreThanFifteenHp(int currentHp, bool expected)
    {
        Assert.Equal(expected, PhantasmalRules.CanBottleSpirits(currentHp));
        Assert.Equal(15, PhantasmalRules.BottleHpLoss);
        Assert.Equal(2, PhantasmalRules.BottledGhosts);
    }

    [Fact]
    public void Procession_AddsTwoApparitions()
    {
        Assert.Equal(2, PhantasmalRules.ProcessionApparitions);
    }

    [Fact]
    public void DropsGhostInAJar_IsDeterministic_PerPlayer_AndAboutHalfTheTime()
    {
        int drops = Enumerable.Range(0, 2000).Count(i => PhantasmalRules.DropsGhostInAJar(42UL, "a0r" + i + "c1", 1UL));
        Assert.InRange(drops, 900, 1100);
        Assert.Equal(PhantasmalRules.DropsGhostInAJar(7UL, "a1r3c2", 5UL), PhantasmalRules.DropsGhostInAJar(7UL, "a1r3c2", 5UL));

        bool anyDifferent = Enumerable.Range(0, 20).Any(i =>
            PhantasmalRules.DropsGhostInAJar(9UL, "a0r" + i + "c0", 1UL) != PhantasmalRules.DropsGhostInAJar(9UL, "a0r" + i + "c0", 2UL));
        Assert.True(anyDifferent);
    }
}
