using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Ferrosand;

namespace ZoneTheSpire.Tests;

public class FerrosandRulesTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(0, 1)]
    public void MagnetizedAmount_IsOnePerPlayer(int players, int expected)
    {
        Assert.Equal(expected, FerrosandRules.MagnetizedAmount(players));
    }

    [Theory]
    [InlineData(12, 0, 1, true)]
    [InlineData(12, 1, 1, false)]
    [InlineData(12, 3, 4, true)]
    [InlineData(12, 4, 4, false)]
    [InlineData(0, 0, 1, false)]
    public void ShouldDampen_OnlyDamagingHitsWhileChargesRemain(int damage, int used, int charges, bool expected)
    {
        Assert.Equal(expected, FerrosandRules.ShouldDampen(damage, used, charges));
    }

    [Theory]
    [InlineData(12, 1)]
    [InlineData(1, 1)]
    public void DampenedHit_TakesOneHp(int damage, int expected)
    {
        Assert.Equal(expected, FerrosandRules.DampenedHpLoss(damage));
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(7, 1)]
    [InlineData(4, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(-2, 0)]
    public void FerroformBlock_IsAQuarterOfTheBlockRemoved(int blocked, int expected)
    {
        Assert.Equal(expected, FerrosandRules.FerroformBlock(blocked));
    }

    [Fact]
    public void CanMagnetize_PlayableCardsThatAreNotMagneticYet()
    {
        Assert.True(FerrosandRules.CanMagnetize(unplayable: false, alreadyMagnetic: false));
        Assert.False(FerrosandRules.CanMagnetize(unplayable: true, alreadyMagnetic: false));
        Assert.False(FerrosandRules.CanMagnetize(unplayable: false, alreadyMagnetic: true));
    }

    [Theory]
    [InlineData(10, 3)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void MagnetizeCount_IsThreeOrWhatIsLeft(int eligible, int expected)
    {
        Assert.Equal(expected, FerrosandRules.MagnetizeCount(eligible));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void CanPull_AllowsThreePulledCardsPerTurn(int pullsThisTurn, bool expected)
    {
        Assert.Equal(3, FerrosandRules.MaxPullsPerTurn);
        Assert.Equal(expected, FerrosandRules.CanPull(pullsThisTurn));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    public void CanPull_WithLargeFieldstone_AllowsSixPulledCardsPerTurn(int pullsThisTurn, bool expected)
    {
        Assert.Equal(3, FerrosandRules.PullLimit(hasLargeFieldstone: false));
        Assert.Equal(6, FerrosandRules.LargeFieldstoneMaxPullsPerTurn);
        Assert.Equal(6, FerrosandRules.PullLimit(hasLargeFieldstone: true));
        Assert.Equal(expected, FerrosandRules.CanPull(pullsThisTurn, hasLargeFieldstone: true));
    }

    [Theory]
    [InlineData(10, 3)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public void LargeFieldstoneSelectCount_IsThreeOrWhatIsLeft(int eligible, int expected)
    {
        Assert.Equal(expected, FerrosandRules.LargeFieldstoneSelectCount(eligible));
    }

    [Fact]
    public void PickPullIndex_IsDeterministicAndInRange()
    {
        Assert.Equal(-1, FerrosandRules.PickPullIndex(5UL, 1UL, 1, 0, 0));
        for (int pull = 0; pull < 30; pull++)
        {
            int index = FerrosandRules.PickPullIndex(99UL, 2UL, 3, pull, 4);
            Assert.InRange(index, 0, 3);
            Assert.Equal(index, FerrosandRules.PickPullIndex(99UL, 2UL, 3, pull, 4));
        }
    }

    [Fact]
    public void ShopMagneticSlots_AreThreeDistinctSortedSlots_OrAllWhenFewer()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var slots = FerrosandRules.PickShopMagneticSlots(seed, "a1r5c2", 1UL, 7);
            Assert.Equal(3, slots.Count);
            Assert.Equal(slots.Distinct().OrderBy(s => s), slots);
            Assert.All(slots, slot => Assert.InRange(slot, 0, 6));
            Assert.Equal(slots, FerrosandRules.PickShopMagneticSlots(seed, "a1r5c2", 1UL, 7));
        }

        Assert.Equal(new[] { 0, 1 }, FerrosandRules.PickShopMagneticSlots(3UL, "loc", 1UL, 2));
        Assert.Empty(FerrosandRules.PickShopMagneticSlots(3UL, "loc", 1UL, 0));
    }

    [Fact]
    public void MagnetizeIndices_AreCountDistinctSortedIndices_OrAllWhenFewer()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var indices = FerrosandRules.PickMagnetizeIndices(seed, "a1r5c2", 1UL, 10, 3);
            Assert.Equal(3, indices.Count);
            Assert.Equal(indices.Distinct().OrderBy(i => i), indices);
            Assert.All(indices, index => Assert.InRange(index, 0, 9));
            Assert.Equal(indices, FerrosandRules.PickMagnetizeIndices(seed, "a1r5c2", 1UL, 10, 3));
        }

        Assert.Single(FerrosandRules.PickMagnetizeIndices(3UL, "loc", 1UL, 2, 1));
        Assert.Empty(FerrosandRules.PickMagnetizeIndices(3UL, "loc", 1UL, 0, 3));
        Assert.Equal(2, FerrosandRules.PickMagnetizeIndices(3UL, "loc", 1UL, 2, 3).Count);
    }

    [Theory]
    [InlineData(11, 1, true)]
    [InlineData(10, 4, false)]
    [InlineData(50, 0, false)]
    public void Attune_NeedsMoreThanTenHp_AndAMagnetizableCard(int currentHp, int magnetizable, bool expected)
    {
        Assert.Equal(expected, FerrosandRules.CanAttune(currentHp, magnetizable));
        Assert.Equal(3, FerrosandRules.AttuneMaxCards);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void ChargeTheIron_AsksForTwoOrWhatIsLeft(int eligible, int expected)
    {
        Assert.Equal(expected, FerrosandRules.ChargeSelectCount(eligible));
        Assert.Equal(4, FerrosandRules.ChargeMaxHpLoss);
    }
}
