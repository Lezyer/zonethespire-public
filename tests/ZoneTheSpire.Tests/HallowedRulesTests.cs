using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Hallowed;

namespace ZoneTheSpire.Tests;

public class HallowedRulesTests
{
    [Theory]
    // Half, rounded up, at least 1 while any Hallowed remains.
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(10, 5)]
    [InlineData(11, 6)]
    public void ConvertToDoom_IsHalfRoundedUp_AtLeastOne(int hallowed, int expected)
    {
        Assert.Equal(expected, HallowedRules.ConvertToDoom(hallowed));
    }

    [Fact]
    public void ConvertToDoom_NeverExceedsTheHallowed()
    {
        for (int hallowed = 1; hallowed <= 60; hallowed++)
        {
            Assert.InRange(HallowedRules.ConvertToDoom(hallowed), 1, hallowed);
        }
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(4, 5, true)]
    [InlineData(6, 5, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, false)]
    public void IsHallowedKill_WhenHpIsAtOrBelowHallowed(int hp, int hallowed, bool expected)
    {
        Assert.Equal(expected, HallowedRules.IsHallowedKill(hp, hallowed));
    }

    [Theory]
    [InlineData(20, true, 3)]
    [InlineData(10, true, 1)]
    [InlineData(7, true, 1)]
    [InlineData(3, true, 1)]
    [InlineData(0, true, 0)]
    [InlineData(10, false, 0)]
    public void ZealousHallowed_Is15PercentOfAttackDamage_RoundedDown_AtLeastOne(int damage, bool powered, int expected)
    {
        Assert.Equal(expected, HallowedRules.ZealousHallowed(damage, powered));
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(10, false)]
    [InlineData(1, false)]
    public void CanRepent_NeedsMoreHpThanItCosts(int hp, bool expected)
    {
        Assert.Equal(expected, HallowedRules.CanRepent(hp));
    }

    [Fact]
    public void Constants_MatchTheSpec()
    {
        Assert.Equal(5, HallowedRules.HallowedPerBlasphemy);
        Assert.Equal(2, HallowedRules.BlasphemousPerTurn);
        Assert.Equal(15, HallowedRules.ZealousPercent);
        Assert.Equal(10, HallowedRules.RepentHpCost);
        Assert.Equal(3, HallowedRules.RepentAttackCount);
        Assert.Equal(3, HallowedRules.ShopHallowingCount);
        Assert.Equal(5, HallowedRules.RedemptionHallowedRemoved);
    }

    [Theory]
    // Redemption removes up to 5 Hallowed: never more than the player has.
    [InlineData(12, 5)]
    [InlineData(5, 5)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void RedemptionRemoval_IsFiveOrWhatIsLeft(int hallowed, int expected)
    {
        Assert.Equal(expected, HallowedRules.RedemptionRemoval(hallowed));
    }

    [Fact]
    public void PickIndices_AreDistinctAscendingInRange_AndDeterministic()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var pick = HallowedRules.PickIndices(seed, "shop:a1.r5.c2.i0", 7UL, 10, 3);
            Assert.Equal(3, pick.Count);
            Assert.Equal(pick.Distinct().OrderBy(i => i), pick);
            Assert.All(pick, i => Assert.InRange(i, 0, 9));
            Assert.Equal(pick, HallowedRules.PickIndices(seed, "shop:a1.r5.c2.i0", 7UL, 10, 3));
        }

        Assert.Equal(2, HallowedRules.PickIndices(3UL, "k", 1UL, 2, 3).Count);
        Assert.Empty(HallowedRules.PickIndices(3UL, "k", 1UL, 0, 3));
        Assert.Empty(HallowedRules.PickIndices(3UL, "k", 1UL, 5, 0));
    }

    [Fact]
    public void PickPreferring_OnlyTakesNonPreferredWhenPreferredRunOut()
    {
        // Indices 1 and 3 already carry the permanent modifier (not preferred).
        var preferred = new List<bool> { true, false, true, false, true };
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var pick = HallowedRules.PickPreferring(seed, "k", 1UL, preferred, 2);
            Assert.Equal(2, pick.Count);
            Assert.All(pick, i => Assert.True(preferred[i]));
        }

        // Only one preferred card: it is always picked, and the second pick comes from the rest.
        var oneFresh = new List<bool> { false, true, false };
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var pick = HallowedRules.PickPreferring(seed, "k", 1UL, oneFresh, 2);
            Assert.Equal(2, pick.Count);
            Assert.Contains(1, pick);
            Assert.Equal(pick.Distinct().OrderBy(i => i), pick);
        }

        // Nothing preferred: falls back to the rest.
        Assert.Equal(2, HallowedRules.PickPreferring(5UL, "k", 1UL, new List<bool> { false, false, false }, 2).Count);
        Assert.Empty(HallowedRules.PickPreferring(5UL, "k", 1UL, new List<bool>(), 2));
    }

    [Fact]
    public void Streams_KeepTurnsLocationsAndPurposesApart()
    {
        Assert.Equal("blasphemous:a1.r5.c2.i0:4", HallowedRules.BlasphemousStream("a1.r5.c2.i0", 4));
        Assert.Equal("shop:a1.r5.c2.i0", HallowedRules.ShopStream("a1.r5.c2.i0"));
        Assert.Equal("repent:a1.r5.c2.i0", HallowedRules.RepentStream("a1.r5.c2.i0"));

        bool anyDifferent = false;
        for (ulong seed = 1; seed <= 40 && !anyDifferent; seed++)
        {
            var turn1 = HallowedRules.PickIndices(seed, HallowedRules.BlasphemousStream("x", 1), 1UL, 8, 2);
            var turn2 = HallowedRules.PickIndices(seed, HallowedRules.BlasphemousStream("x", 2), 1UL, 8, 2);
            anyDifferent = !turn1.SequenceEqual(turn2);
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void HallowedText_IsShortAndTakesItsNumbersFromTheRules()
    {
        Assert.Equal("At the end of its turn, if it has at least as much [gold]Hallowed[/gold] as HP, it dies.", HallowedText.HallowedDescription);
        Assert.Equal("Each turn, [blue]2[/blue] cards in your [gold]Hand[/gold] become [gold]Blasphemous[/gold].", HallowedText.BlasphemerDescription);
        Assert.Equal("When played, gain [blue]5[/blue] [gold]Hallowed[/gold].", HallowedText.BlasphemousDescription);
        Assert.Equal("Its attacks also apply [gold]Hallowed[/gold] equal to [blue]15%[/blue] of their damage, even when blocked.", HallowedText.ZealousDescription);
        Assert.Equal("Applies [gold]Hallowed[/gold] equal to the damage it deals.", HallowedText.HallowingDescription);
        Assert.Equal("When played, lose [blue]5[/blue] [gold]Hallowed[/gold].", HallowedText.RedemptionDescription);
        Assert.Equal("[gold]Redemption[/gold].", HallowedText.CardLine(HallowedText.Redemption));
    }

    [Fact]
    public void BlindingHallowed_Glossary_ExplainsEachOfItsMechanicsOnce()
    {
        var biome = new ZoneTheSpire.Core.Biomes.HallowedBiome();

        Assert.Equal(
            new[] { "Blasphemer", "Blasphemous", "Zealous", "Hallowing", "Redemption", "Hallowed" },
            biome.Keywords.Select(keyword => keyword.Name));
        Assert.Equal(HallowedText.HallowedDescription, biome.Keywords.Single(keyword => keyword.Name == "Hallowed").Description);
        Assert.Null(biome.TooltipFooter);
    }
}
