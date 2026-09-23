using System.Collections.Generic;
using Xunit;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Tests;

public class MirrorRulesTests
{
    [Theory]
    [InlineData(true, 50, 100, false, true)]
    [InlineData(true, 51, 100, false, false)]
    [InlineData(true, 1, 100, false, true)]
    [InlineData(true, 5, 11, false, true)]
    [InlineData(true, 6, 11, false, false)]
    [InlineData(false, 0, 100, false, false)]
    [InlineData(true, 40, 100, true, false)]
    [InlineData(true, 0, 0, false, false)]
    public void ShouldSplit_AtOrBelowHalf_OnceAndOnlyWhileAlive(bool isAlive, int currentHp, int maxHp, bool hasSplit, bool expected)
    {
        Assert.Equal(expected, MirrorRules.ShouldSplit(isAlive, currentHp, maxHp, hasSplit));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 25)]
    [InlineData(2, 50)]
    [InlineData(5, 50)]
    public void CloneHpPercent_IsQuarterWhenAlone_HalfWithOtherMirrorableEnemies(int startingMirrorableEnemies, int expected)
    {
        Assert.Equal(expected, MirrorRules.CloneHpPercent(startingMirrorableEnemies));
    }

    [Theory]
    [InlineData(0, 25, 1)]
    [InlineData(1, 25, 1)]
    [InlineData(4, 25, 1)]
    [InlineData(5, 25, 2)]
    [InlineData(100, 25, 25)]
    [InlineData(101, 25, 26)]
    [InlineData(250, 25, 63)]
    [InlineData(1, 50, 1)]
    [InlineData(100, 50, 50)]
    [InlineData(101, 50, 51)]
    public void CloneMaxHp_IsPercentRoundedUp_AtLeastOne(int originalMaxHp, int percent, int expected)
    {
        Assert.Equal(expected, MirrorRules.CloneMaxHp(originalMaxHp, percent));
    }

    [Theory]
    [InlineData(46, 15, 53)]
    [InlineData(40, 15, 46)]
    [InlineData(52, 15, 60)]
    [InlineData(1, 15, 2)]
    [InlineData(0, 15, 0)]
    [InlineData(100, 0, 100)]
    public void BoostedMaxHp_AddsPercentRoundedUp(int maxHp, int percent, int expected)
    {
        Assert.Equal(expected, MirrorRules.BoostedMaxHp(maxHp, percent));
    }

    [Fact]
    public void SegmentMaxHpBonusPercent_IsFifteen()
    {
        Assert.Equal(15, MirrorRules.SegmentMaxHpBonusPercent);
    }

    [Fact]
    public void PickMirroredIndex_ReturnsMinusOne_WithoutCandidates()
    {
        Assert.Equal(-1, MirrorRules.PickMirroredIndex(123UL, 1, 0));
    }

    [Fact]
    public void PickMirroredIndex_ReturnsZero_ForSingleCandidate()
    {
        Assert.Equal(0, MirrorRules.PickMirroredIndex(123UL, 7, 1));
    }

    [Fact]
    public void PickMirroredIndex_IsDeterministicAndInRange()
    {
        for (int room = 0; room < 200; room++)
        {
            int first = MirrorRules.PickMirroredIndex(987654321UL, room, 4);
            Assert.Equal(first, MirrorRules.PickMirroredIndex(987654321UL, room, 4));
            Assert.InRange(first, 0, 3);
        }
    }

    [Fact]
    public void PickMirroredIndex_VariesAcrossRoomsAndSeeds()
    {
        var byRoom = new HashSet<int>();
        var bySeed = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            byRoom.Add(MirrorRules.PickMirroredIndex(42UL, i, 4));
            bySeed.Add(MirrorRules.PickMirroredIndex((ulong)i * 7919UL, 3, 4));
        }

        Assert.Equal(4, byRoom.Count);
        Assert.Equal(4, bySeed.Count);
    }

    [Fact]
    public void PickGlamIndex_ReturnsMinusOne_WithoutEligibleOptions()
    {
        Assert.Equal(-1, MirrorRules.PickGlamIndex(1UL, "a0:r1:c2:id0", 7UL, 0));
    }

    [Fact]
    public void PickGlamIndex_ReturnsZero_ForSingleEligibleOption()
    {
        Assert.Equal(0, MirrorRules.PickGlamIndex(1UL, "a0:r1:c2:id0", 7UL, 1));
    }

    [Fact]
    public void PickGlamIndex_IsDeterministicAndInRange()
    {
        for (int row = 0; row < 100; row++)
        {
            string location = $"a1:r{row}:c3:id0";
            int first = MirrorRules.PickGlamIndex(555UL, location, 9UL, 3);
            Assert.Equal(first, MirrorRules.PickGlamIndex(555UL, location, 9UL, 3));
            Assert.InRange(first, 0, 2);
        }
    }

    [Fact]
    public void PickGlamIndex_VariesAcrossLocationsAndPlayers()
    {
        var byLocation = new HashSet<int>();
        var byPlayer = new HashSet<int>();
        for (int i = 0; i < 100; i++)
        {
            byLocation.Add(MirrorRules.PickGlamIndex(42UL, $"a0:r{i}:c0:id0", 1UL, 3));
            byPlayer.Add(MirrorRules.PickGlamIndex(42UL, "a0:r5:c0:id0", (ulong)i, 3));
        }

        Assert.Equal(3, byLocation.Count);
        Assert.Equal(3, byPlayer.Count);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(6, false)]
    [InlineData(1, false)]
    public void ShakeHands_NeedsMoreMaxHpThanItCosts(int maxHp, bool expected)
    {
        Assert.Equal(expected, MirrorRules.CanShakeHands(maxHp));
        Assert.Equal(6, MirrorRules.ShakeHandsMaxHpLoss);
        Assert.Equal(1, MirrorRules.TurnAwayGlamCount);
    }
}
