using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Fermentory;

namespace ZoneTheSpire.Tests;

public class FermentingVatRulesTests
{
    [Fact]
    public void Constants_MatchTheDesign()
    {
        Assert.Equal(10, FermentingVatRules.DrinkMaxHp);
        Assert.Equal(1, FermentingVatRules.UpgradesPerPotion);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(3, 10, true)]
    [InlineData(0, 10, false)]
    [InlineData(2, 0, false)]
    public void CanFeed_NeedsAPotionAndAnUpgradableCard(int potions, int upgradable, bool expected) =>
        Assert.Equal(expected, FermentingVatRules.CanFeed(potions, upgradable));

    [Theory]
    [InlineData(3, 10, 3)]
    [InlineData(1, 10, 1)]
    [InlineData(5, 2, 2)]
    [InlineData(0, 4, 0)]
    public void FeedUpgrades_IsOnePerPotion_CappedByUpgradableCards(int potions, int upgradable, int expected) =>
        Assert.Equal(expected, FermentingVatRules.FeedUpgrades(potions, upgradable));

    [Fact]
    public void PickUpgrades_IsSeeded_DistinctAndInRange()
    {
        var first = FermentingVatRules.PickUpgrades(7UL, "loc", 1UL, 12, 3);
        Assert.Equal(first, FermentingVatRules.PickUpgrades(7UL, "loc", 1UL, 12, 3));
        Assert.Equal(3, first.Distinct().Count());
        Assert.All(first, index => Assert.InRange(index, 0, 11));
        Assert.Equal(first.OrderBy(i => i), first);
        Assert.Equal(new[] { 0, 1 }, FermentingVatRules.PickUpgrades(7UL, "loc", 1UL, 2, 5));
        Assert.Empty(FermentingVatRules.PickUpgrades(7UL, "loc", 1UL, 0, 3));
    }
}

public class BrewExtractorRulesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Offers_WheneverTheScreenHasRoom(int existing, bool expected) =>
        Assert.Equal(expected, ZoneTheSpire.Core.ZoneRelics.ZoneRelicEffects.BrewExtractorOffers(existing));

    [Fact]
    public void IsTheFermentorysChestRelic() =>
        Assert.Equal(
            new[] { ZoneTheSpire.Core.ZoneRelics.ZoneRelicCatalog.BrewExtractorKey },
            ZoneTheSpire.Core.ZoneRelics.ZoneRelicCatalog.KeysFor("fermentory"));
}
