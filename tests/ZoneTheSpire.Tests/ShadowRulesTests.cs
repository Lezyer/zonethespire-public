using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Tests;

public class ShadowRulesTests
{
    [Fact]
    public void Brutality_GivesDoomFor50PercentOfAttacks_RoundedDown_AtLeast1()
    {
        Assert.Equal(10, ShadowRules.BrutalityDoom(20m, poweredAttack: true));
        Assert.Equal(7, ShadowRules.BrutalityDoom(15m, poweredAttack: true));
        Assert.Equal(1, ShadowRules.BrutalityDoom(3m, poweredAttack: true));
        Assert.Equal(0, ShadowRules.BrutalityDoom(0m, poweredAttack: true));
        Assert.Equal(0, ShadowRules.BrutalityDoom(20m, poweredAttack: false));
    }

    [Theory]
    [InlineData(25, 15)]
    [InlineData(10, 0)]
    [InlineData(4, 0)]
    public void LightTheWay_Removes10Doom_NeverBelowZero(int doom, int expected)
    {
        Assert.Equal(expected, ShadowRules.DoomAfterLight(doom));
    }

    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(true, false, true, 1)]
    [InlineData(false, true, true, 1)]
    [InlineData(true, true, true, 2)]
    public void Lantern_HidesSightEverywhere_AndShadesOneMoreCardPerTurn(bool inShadowFight, bool holdsLantern, bool sight, int shades)
    {
        Assert.Equal(sight, ShadowRules.ShadowSightApplies(inShadowFight, holdsLantern));
        Assert.Equal(shades, ShadowRules.ShadesPerTurn(inShadowFight, holdsLantern));
    }

    [Fact]
    public void PickShadeIndex_FirstPickIsUnchanged_LaterPicksUseTheirOwnStream()
    {
        Assert.Equal(ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, 3, 6), ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, 3, 6, 0));
        Assert.Contains(Enumerable.Range(1, 30), turn =>
            ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, turn, 6, 0) != ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, turn, 6, 1));
    }

    [Theory]
    [InlineData(12, 5, 7)]
    [InlineData(3, 5, 0)]
    [InlineData(0, 5, 0)]
    public void LanternLight_Removes5Doom_NeverBelowZero(int doom, int removed, int expected)
    {
        Assert.Equal(expected, ShadowRules.DoomAfterRemoving(doom, removed));
        Assert.Equal(5, ShadowRules.LanternLightDoomRemoved);
    }

    [Fact]
    public void TroubledDreamsCurse_IsDeterministic_AndRoughlyHalfTheTime()
    {
        int curses = 0;
        for (ulong seed = 0; seed < 1000; seed++)
        {
            bool curse = ShadowRules.TroubledDreamsCurses(seed, "a1r8c2", 7UL);
            Assert.Equal(curse, ShadowRules.TroubledDreamsCurses(seed, "a1r8c2", 7UL));
            curses += curse ? 1 : 0;
        }

        Assert.InRange(curses, 420, 580);
    }

    [Fact]
    public void Shading_SkipsStatusesCursesAndShadedCards()
    {
        Assert.True(ShadowRules.CanShade(isStatusOrCurse: false, alreadyShaded: false));
        Assert.False(ShadowRules.CanShade(isStatusOrCurse: true, alreadyShaded: false));
        Assert.False(ShadowRules.CanShade(isStatusOrCurse: false, alreadyShaded: true));
    }

    [Fact]
    public void PickShadeIndex_IsDeterministic_InRange_AndVariesByTurnAndPlayer()
    {
        Assert.Equal(-1, ShadowRules.PickShadeIndex(1UL, "a0r3c2", 7UL, 1, 0));
        for (int turn = 1; turn < 30; turn++)
        {
            int index = ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, turn, 5);
            Assert.InRange(index, 0, 4);
            Assert.Equal(index, ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, turn, 5));
        }

        Assert.Contains(Enumerable.Range(1, 30), turn =>
            ShadowRules.PickShadeIndex(42UL, "a0r3c2", 7UL, turn, 5) != ShadowRules.PickShadeIndex(42UL, "a0r3c2", 8UL, turn, 5));
    }

    [Theory]
    [InlineData(7, 4)]
    [InlineData(4, 4)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void ShopShadedSlots_AreFourDistinctSlots_OrEveryCardWhenFewer(int cardSlots, int expectedCount)
    {
        var slots = ShadowRules.PickShopShadedSlots(99UL, "a1r5c4", 3UL, cardSlots);
        Assert.Equal(expectedCount, slots.Count);
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.All(slots, slot => Assert.InRange(slot, 0, cardSlots - 1));
        Assert.Equal(slots.OrderBy(slot => slot), slots);
        Assert.Equal(slots, ShadowRules.PickShopShadedSlots(99UL, "a1r5c4", 3UL, cardSlots));
    }

    [Theory]
    [InlineData(150, 60)]
    [InlineData(75, 30)]
    [InlineData(51, 20)]
    [InlineData(0, 0)]
    public void DiscountedPrice_Is60PercentOff_RoundedToNearest(int cost, int expected)
    {
        Assert.Equal(expected, ShadowRules.DiscountedPrice(cost));
    }

    [Theory]
    [InlineData(1, 25, 8, false)]
    [InlineData(2, 50, 16, false)]
    [InlineData(3, 75, 24, true)]
    [InlineData(5, 125, 40, true)]
    public void Dawn_PaysPerNode_AndARelicFromThreeNodes(int nodes, int gold, int heal, bool relic)
    {
        Assert.Equal(gold, ShadowRules.DawnGold(nodes));
        Assert.Equal(heal, ShadowRules.DawnHeal(80, nodes));
        Assert.Equal(relic, ShadowRules.DawnGivesRelic(nodes));
    }

    [Fact]
    public void NodeSets_AreOrderedUnions_WithoutDuplicates()
    {
        string once = ShadowRules.AddToNodeSet(string.Empty, new[] { ShadowRules.NodeKey(0, 5, 3), ShadowRules.NodeKey(0, 4, 2) });
        Assert.Equal("0:4:2;0:5:3", once);
        string twice = ShadowRules.AddToNodeSet(once, new[] { "0:5:3", "1:2:1", "" });
        Assert.Equal("0:4:2;0:5:3;1:2:1", twice);
        Assert.Equal(3, ShadowRules.ParseNodeSet(twice).Count);
        Assert.Empty(ShadowRules.ParseNodeSet(null));
    }

    [Fact]
    public void HiddenNodeTooltip_NamesEveryNodeKind_AsLinksToWhatTheyDoHere()
    {
        var biome = new ShadowCorruptionBiome();
        string tip = ZoneTooltip.BuildHiddenDescription(biome, "3B2352", _ => null);
        string[] lines = tip.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal(
            "[color=#3B2352]Shadow Corruption[/color]: one of these may be hiding here: [gold]Fight[/gold], [gold]Rest Site[/gold], [gold]Shop[/gold], [gold]Unknown[/gold] or [gold]Chest[/gold].",
            lines[0]);
        Assert.Contains("Leaving the zone gives", lines[1]);

        var keywords = biome.Keywords.ToDictionary(keyword => keyword.Name, keyword => keyword.Description);
        Assert.StartsWith("Intents are hidden and enemies have", keywords["Fight"]);
        Assert.Contains("Light the Way", keywords["Fight"]);
        Assert.StartsWith("[gold]Rest[/gold] becomes [gold]Troubled Dreams[/gold]", keywords["Rest Site"]);
        Assert.StartsWith("Relics, potions and", keywords["Shop"]);
        Assert.StartsWith("If it's a fight: intents are hidden", keywords["Unknown"]);
        Assert.DoesNotContain("{Zone}", string.Concat(keywords.Values));
        Assert.Equal(new[] { "Fight", "Rest Site", "Shop", "Unknown", "Chest" }, ZoneTooltip.KeywordsIn(biome, lines[0]).Select(keyword => keyword.Name).Take(5));
    }

    [Fact]
    public void EveryShadowNodeTooltip_EndsWithTheDawnRewards_EvenWithoutAnEffect()
    {
        var biome = new ShadowCorruptionBiome();
        foreach (NodeKind kind in new[] { NodeKind.Monster, NodeKind.Shop, NodeKind.RestSite, NodeKind.Treasure })
        {
            string? tip = ZoneTooltip.BuildDescription(biome, kind, "9A82C2", _ => null);
            Assert.NotNull(tip);
            Assert.Contains("[gold]25 Gold[/gold]", tip!.Split('\n')[^1]);
        }

        Assert.Equal("9A82C2", biome.TooltipColorHex);
    }

    [Fact]
    public void Biome_AffectsFightsShopsCampfiresAndChests()
    {
        var biome = new ShadowCorruptionBiome();
        Assert.NotEmpty(ZoneEffects.EffectsFor(biome, NodeKind.Monster));
        Assert.NotEmpty(ZoneEffects.EffectsFor(biome, NodeKind.Unknown));
        Assert.NotEmpty(ZoneEffects.EffectsFor(biome, NodeKind.Shop));
        Assert.NotEmpty(ZoneEffects.EffectsFor(biome, NodeKind.RestSite));
        Assert.Equal(new[] { ZoneRelicCatalog.ChestRelicEffect }, ZoneEffects.EffectsFor(biome, NodeKind.Treasure));
    }
}
