using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

public class ZoneTooltipTests
{
    private static string? GlamResolver(string token) => token == "Glam" ? "[gold]Glam[/gold]" : null;

    private sealed class TwoLineBiome : BiomeDefinition
    {
        public override string Id => "test_lines";

        public override string DisplayName => "Test Zone";

        public override string ColorHex => "123456";

        public override IReadOnlyList<BiomeEffect> Effects { get; } = new[]
        {
            new BiomeEffect("test.one", new[] { NodeKind.RestSite }, "{Zone} rests are odd."),
            new BiomeEffect("test.two", new[] { NodeKind.RestSite, NodeKind.Shop }, "Prices {Other}."),
        };
    }

    [Theory]
    [InlineData(NodeKind.Monster)]
    [InlineData(NodeKind.Elite)]
    [InlineData(NodeKind.Unknown)]
    public void Mirrorlands_BuildsTheColouredSentence(NodeKind kind)
    {
        string? text = ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, kind, "8FD3F4", GlamResolver);

        Assert.Equal(
            "[color=#8FD3F4]Mirrorlands[/color]: one enemy is [gold]Mirrored[/gold]. [blue]1[/blue] card in each card reward has [gold]Glam[/gold].",
            text);
    }

    [Fact]
    public void ZoneToken_UsesTheGivenOutlineHex()
    {
        string? text = ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.Monster, "ABCDEF", GlamResolver);
        Assert.StartsWith("[color=#ABCDEF]Mirrorlands[/color]", text);
    }

    [Fact]
    public void UnresolvedTokens_AreKept()
    {
        string? text = ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.Monster, "8FD3F4", _ => null);
        Assert.EndsWith("[blue]1[/blue] card in each card reward has {Glam}.", text);
    }

    [Fact]
    public void ColourTags_AreNotTreatedAsTokens()
    {
        string? text = ZoneTooltip.BuildDescription(BiomeRegistry.Get("scrapyard")!, NodeKind.Shop, "8B5A2B", _ => null);
        Assert.Equal("[color=#8B5A2B]The Scrapyard[/color]: the merchant offers [gold]Scrap[/gold] instead of card removal.", text);
    }

    [Fact]
    public void MultipleApplicableEffects_AreJoinedWithASpace()
    {
        Func<string, string?> resolver = token => token == "Other" ? "rise" : null;

        Assert.Equal(
            "[color=#654321]Test Zone[/color] rests are odd. Prices rise.",
            ZoneTooltip.BuildDescription(new TwoLineBiome(), NodeKind.RestSite, "654321", resolver));
        Assert.Equal("Prices rise.", ZoneTooltip.BuildDescription(new TwoLineBiome(), NodeKind.Shop, "654321", resolver));
    }

    [Fact]
    public void LaterEffectLines_DropTheirZonePrefix_AndStartASentence()
    {
        Assert.Equal(
            "[color=#F2D14B]Blinding Hallows[/color]: you can [gold]Repent[/gold]: lose [red]10[/red] HP to remove all [red]Curses[/red] and give [blue]3[/blue] random attacks [gold]Hallowing[/gold]. [gold]Smith[/gold] becomes [gold]Blaspheme[/gold]: attacks also gain [gold]Hallowing[/gold] and [gold]Blasphemous[/gold].",
            ZoneTooltip.BuildDescription(BiomeRegistry.Get("blinding_hallowed")!, NodeKind.RestSite, "F2D14B", _ => null));
        Assert.Equal(
            "[color=#654321]Test Zone[/color]: a. [blue]B[/blue] c.",
            ZoneTooltip.BuildDescription(new PrefixedBiome(), NodeKind.Shop, "654321", _ => null));
    }

    [Fact]
    public void KeywordsIn_ListsNamedKeywords_ThenTheKeywordsTheirTipsName_EachOnce()
    {
        var biome = BiomeRegistry.Get("blinding_hallowed")!;
        string fights = ZoneTooltip.BuildDescription(biome, NodeKind.Monster, "F2D14B", _ => null)!;

        Assert.Equal(
            new[] { "Blasphemer", "Zealous", "Hallowing", "Redemption", "Blasphemous", "Hallowed" },
            ZoneTooltip.KeywordsIn(biome, fights).Select(keyword => keyword.Name));
        Assert.Equal(
            new[] { "Hallowing", "Hallowed" },
            ZoneTooltip.KeywordsIn(biome, ZoneTooltip.BuildDescription(biome, NodeKind.Shop, "F2D14B", _ => null)!).Select(keyword => keyword.Name));
        Assert.Empty(ZoneTooltip.KeywordsIn(biome, "Hallowed without gold tags."));
        Assert.Empty(ZoneTooltip.KeywordsIn(BiomeRegistry.Get("mirrorlands")!, fights));
    }

    [Fact]
    public void EveryGlossaryKeyword_IsReachableFromItsZonesMapLines()
    {
        foreach (BiomeDefinition biome in BiomeRegistry.All.Where(biome => biome.Keywords.Count > 0))
        {
            string lines = string.Join(" ", biome.Effects.Select(effect => effect.TooltipLine));
            if (biome is ShadowCorruptionBiome)
            {
                // Its hidden nodes' tip links the node kinds.
                lines += " " + ZoneTooltip.HiddenNodeLine;
            }

            Assert.Equal(
                biome.Keywords.Select(keyword => keyword.Name).OrderBy(name => name),
                ZoneTooltip.KeywordsIn(biome, lines).Select(keyword => keyword.Name).OrderBy(name => name));
        }
    }

    [Fact]
    public void GlossaryKeywordNames_AreUniqueAcrossZones()
    {
        var names = BiomeRegistry.All.SelectMany(biome => biome.Keywords).Select(keyword => keyword.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    private sealed class PrefixedBiome : BiomeDefinition
    {
        public override string Id => "test_prefixed";

        public override string DisplayName => "Test Zone";

        public override string ColorHex => "654321";

        public override IReadOnlyList<BiomeEffect> Effects { get; } = new[]
        {
            new BiomeEffect("test.a", new[] { NodeKind.Shop }, "{Zone}: a."),
            new BiomeEffect("test.b", new[] { NodeKind.Shop }, "{Zone}: [blue]b[/blue] c."),
        };
    }

    [Fact]
    public void NoApplicableEffect_ReturnsNull()
    {
        Assert.Null(ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.Other, "8FD3F4", GlamResolver));
    }

    [Fact]
    public void Mirrorlands_Treasure_DescribesZoneRelicChance()
    {
        Assert.Equal(
            "[color=#8FD3F4]Mirrorlands[/color]: the chest has a [blue]50%[/blue] chance to also offer a zone relic.",
            ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.Treasure, "8FD3F4", GlamResolver));
    }

    [Fact]
    public void Mirrorlands_Shop_DescribesDuplicate()
    {
        Assert.Equal(
            "[color=#8FD3F4]Mirrorlands[/color]: the merchant offers [gold]Duplicate[/gold] instead of card removal.",
            ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.Shop, "8FD3F4", GlamResolver));
    }

    [Fact]
    public void Mirrorlands_RestSite_DescribesMirroredCampfire()
    {
        Assert.Equal(
            "[color=#8FD3F4]Mirrorlands[/color]: [gold]Rest[/gold] and [gold]Mend[/gold] also let you duplicate [blue]1[/blue] of [blue]3[/blue] random cards.",
            ZoneTooltip.BuildDescription(BiomeRegistry.Get("mirrorlands")!, NodeKind.RestSite, "8FD3F4", GlamResolver));
    }
}
