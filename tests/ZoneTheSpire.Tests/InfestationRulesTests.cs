using Xunit;
using ZoneTheSpire.Core.Infestation;

namespace ZoneTheSpire.Tests;

public class InfestationRulesTests
{
    [Fact]
    public void EnemyMonsterDeath_SpawnsWriggler()
    {
        Assert.True(InfestationRules.ShouldSpawnWriggler(isEnemyMonster: true, isMinion: false, isPhrogParasite: false, spawnedByInfestation: false, alreadyReleased: false));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void PlayersMinionsParasitesInfestationWrigglersAndRepeatDeaths_DoNotSpawn(
        bool isEnemyMonster, bool isMinion, bool isPhrogParasite, bool spawnedByInfestation, bool alreadyReleased)
    {
        Assert.False(InfestationRules.ShouldSpawnWriggler(isEnemyMonster, isMinion, isPhrogParasite, spawnedByInfestation, alreadyReleased));
    }

    [Theory]
    [InlineData(0, "NASTY_BITE_MOVE")]
    [InlineData(1, "WRIGGLE_MOVE")]
    [InlineData(2, "NASTY_BITE_MOVE")]
    [InlineData(7, "WRIGGLE_MOVE")]
    public void InitialMove_AlternatesBiteAndWriggleBySpawnOrder(int spawnIndex, string expected)
    {
        Assert.Equal(expected, InfestationRules.InitialMoveId(spawnIndex));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 60)]
    public void WrigglerHpBonus_Is15_30_60PercentByAct(int actIndex, int expected)
    {
        Assert.Equal(expected, InfestationRules.WrigglerHpBonusPercent(actIndex));
    }

    [Theory]
    [InlineData(20, 0, 23)]
    [InlineData(20, 1, 26)]
    [InlineData(20, 2, 32)]
    [InlineData(17, 0, 20)]
    [InlineData(10, 0, 12)]
    public void BoostedWrigglerMaxHp_AddsTheActBonusRounded(int maxHp, int actIndex, int expected)
    {
        Assert.Equal(expected, InfestationRules.BoostedWrigglerMaxHp(maxHp, actIndex));
    }

    [Theory]
    [InlineData(13, true)]
    [InlineData(12, false)]
    [InlineData(1, false)]
    public void ReachIn_NeedsMoreThanTwelveHp(int currentHp, bool expected)
    {
        Assert.Equal(expected, InfestationRules.CanReachIn(currentHp));
    }

    [Fact]
    public void LetThemNest_TakesUpToThreePlayableCards()
    {
        Assert.Equal(3, InfestationRules.NestMaxCards);
        Assert.True(InfestationRules.CanNest(unplayable: false));
        Assert.False(InfestationRules.CanNest(unplayable: true));
    }
}
