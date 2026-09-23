using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Tests;

public class TribunalRulesTests
{
    [Fact]
    public void Numbers_MatchTheDesign()
    {
        Assert.Equal(10, TribunalRules.ConfessHpCost);
        Assert.Equal(2, TribunalRules.MaxHpPerRedemption);
        Assert.Equal(2, TribunalRules.DevoteDowngrades);
        Assert.Equal(2, TribunalRules.InquisitorsWrathCost);
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(10, false)]
    [InlineData(1, false)]
    public void Confess_NeedsMoreHpThanItCosts(int hp, bool expected)
    {
        Assert.Equal(expected, TribunalRules.CanConfess(hp));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(4, 8)]
    [InlineData(-3, 0)]
    public void Confess_GivesTwoMaxHpPerRedemptionLost(int lost, int expected)
    {
        Assert.Equal(expected, TribunalRules.ConfessMaxHp(lost));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Deny_NeedsACardToUpgrade(int upgradable, bool expected)
    {
        Assert.Equal(expected, TribunalRules.CanDeny(upgradable));
    }

    [Theory]
    [InlineData(true, false, true, true)]    // upgraded by Deny, no Redemption: Blasphemous
    [InlineData(true, true, true, false)]    // Redemption: upgraded for free
    [InlineData(false, false, true, false)]  // already upgraded before: untouched
    [InlineData(true, false, false, false)]  // can't carry it (unplayable, curse, status)
    public void Deny_BlasphemesOnlyTheCardsItUpgraded_WithoutRedemption(bool upgradedByDeny, bool hasRedemption, bool canCarry, bool expected)
    {
        Assert.Equal(expected, TribunalRules.DenyBlasphemes(upgradedByDeny, hasRedemption, canCarry));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void Devote_NeedsTwoUpgradedCards(int upgraded, bool expected)
    {
        Assert.Equal(expected, TribunalRules.CanDevote(upgraded));
    }

    [Fact]
    public void Devote_DowngradesTwoDistinctSeededCards()
    {
        IReadOnlyList<int> picks = TribunalRules.PickDowngrades(7UL, "1:5:3", 2UL, 6);

        Assert.Equal(2, picks.Count);
        Assert.Equal(2, picks.Distinct().Count());
        Assert.All(picks, index => Assert.InRange(index, 0, 5));
        Assert.Equal(picks, TribunalRules.PickDowngrades(7UL, "1:5:3", 2UL, 6));
    }
}

public class SacrosanctFlailRulesTests
{
    [Fact]
    public void Numbers_MatchTheDesign()
    {
        Assert.Equal(3, ZoneRelicEffects.SacrosanctFlailAttacks);
        Assert.Equal(2, ZoneRelicEffects.SacrosanctFlailMultiplier);
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(1, 2)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    public void AppliesTwiceTheHallowedGained(int gained, int expected)
    {
        Assert.Equal(expected, ZoneRelicEffects.SacrosanctFlailHallowed(gained));
    }

    [Fact]
    public void PickupPicks_PreferAttacksWithoutTheModifiers()
    {
        // Attacks 1, 3 and 4 have neither modifier yet: all three are picked before any other.
        var fresh = new List<bool> { false, true, false, true, true, false };
        IReadOnlyList<int> picks = ZoneRelicEffects.PickSacrosanctFlailAttacks(11UL, 4UL, fresh);

        Assert.Equal(new[] { 1, 3, 4 }, picks.OrderBy(i => i));
        Assert.Equal(picks, ZoneRelicEffects.PickSacrosanctFlailAttacks(11UL, 4UL, fresh));
    }

    [Fact]
    public void PickupPicks_TakeWhatThereIs_WithFewerAttacks()
    {
        Assert.Equal(2, ZoneRelicEffects.PickSacrosanctFlailAttacks(11UL, 4UL, new List<bool> { true, false }).Count);
        Assert.Empty(ZoneRelicEffects.PickSacrosanctFlailAttacks(11UL, 4UL, new List<bool>()));
    }

    [Fact]
    public void Catalog_BlindingHallowedOffersTheSacrosanctFlail()
    {
        Assert.Equal(new[] { ZoneRelicCatalog.SacrosanctFlailKey }, ZoneRelicCatalog.KeysFor(new HallowedBiome().Id));
    }

    [Fact]
    public void BlindingHallowed_ChestNodes_AdvertiseTheZoneRelic()
    {
        Assert.Equal(
            "[color=#F2D14B]Blinding Hallows[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(BiomeRegistry.Get("blinding_hallowed")!, NodeKind.Treasure, "F2D14B", _ => null));
    }
}
