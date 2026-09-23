using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Tooltips;

namespace ZoneTheSpire.Tests;

public class NestedTooltipTextTests
{
    private static readonly string[] Linkable = { "Hallowed", "Doom", "Blasphemous" };

    [Fact]
    public void Linkify_WrapsGoldKeywordsInLinks()
    {
        Assert.Equal(
            "When played, gain [blue]5[/blue] [url=zts:Hallowed][gold]Hallowed[/gold][/url].",
            NestedTooltipText.Linkify("When played, gain [blue]5[/blue] [gold]Hallowed[/gold].", Linkable, exclude: null));
    }

    [Fact]
    public void Linkify_LeavesUnknownPlainAndExcludedKeywordsAlone()
    {
        Assert.Equal(
            "[gold]Hallowed[/gold] and [url=zts:Doom][gold]Doom[/gold][/url], not Blasphemous or [gold]Block[/gold].",
            NestedTooltipText.Linkify("[gold]Hallowed[/gold] and [gold]Doom[/gold], not Blasphemous or [gold]Block[/gold].", Linkable, exclude: "Hallowed"));
    }

    [Fact]
    public void Linkify_DoesNotDoubleWrap()
    {
        string once = NestedTooltipText.Linkify("[gold]Doom[/gold]", Linkable, null);
        Assert.Equal(once, NestedTooltipText.Linkify(once, Linkable, null));
    }

    [Theory]
    [InlineData("zts:Hallowed", "Hallowed")]
    [InlineData("Hallowed", null)]
    [InlineData("zts:", null)]
    public void KeywordOf_ReadsOurLinksOnly(string meta, string? expected)
    {
        Assert.Equal(expected, NestedTooltipText.KeywordOf(meta));
    }

    [Fact]
    public void Mentions_FindsATitleOrGoldKeyword()
    {
        string[] mod = { "Hallowed" };
        Assert.True(NestedTooltipText.Mentions("Hallowed", "anything", mod));
        Assert.True(NestedTooltipText.Mentions("Strike", "Apply [gold]Hallowed[/gold].", mod));
        Assert.False(NestedTooltipText.Mentions("Doom", "Hallowed without tags, [gold]Doom[/gold].", mod));
        Assert.False(NestedTooltipText.Mentions(null, "", mod));
    }

    [Fact]
    public void LinkedKeywords_ListsLinksInReadingOrder_EachOnce()
    {
        string text = NestedTooltipText.Linkify("[gold]Doom[/gold], [gold]Hallowed[/gold] and [gold]Doom[/gold] again.", Linkable, null);
        Assert.Equal(new[] { "Doom", "Hallowed" }, NestedTooltipText.LinkedKeywords(text));
        Assert.Empty(NestedTooltipText.LinkedKeywords("[gold]Doom[/gold]"));
    }

    [Fact]
    public void Highlight_MarksTheFirstLinkOfTheKeywordOnly()
    {
        string text = NestedTooltipText.Linkify("[gold]Doom[/gold] and [gold]Doom[/gold], [gold]Hallowed[/gold].", Linkable, null);
        Assert.Equal(
            "[url=zts:Doom][bgcolor=#F2D14B66][color=#FFFFFF]Doom[/color][/bgcolor][/url] and [url=zts:Doom][gold]Doom[/gold][/url], [url=zts:Hallowed][gold]Hallowed[/gold][/url].",
            NestedTooltipText.Highlight(text, "Doom"));
        Assert.Equal(text, NestedTooltipText.Highlight(text, "Blasphemous"));
        Assert.Equal(text, NestedTooltipText.Highlight(text, null));
    }

    [Theory]
    [InlineData(0, 1, 3, 1)]
    [InlineData(2, 1, 3, 0)]
    [InlineData(0, -1, 3, 2)]
    [InlineData(0, 1, 0, -1)]
    public void Cycle_WrapsAround(int index, int step, int count, int expected)
    {
        Assert.Equal(expected, NestedTooltipText.Cycle(index, step, count));
    }

    [Fact]
    public void Links_KeepNamesWithApostrophesAndSpacesOutOfTheTag()
    {
        // BBCode reads a quote in a tag value as the start of a quoted value: Deva's Blessing must be encoded in the link.
        string[] names = { "Deva's Blessing", "Touch of Midas" };
        string text = NestedTooltipText.Linkify("Enemies have [gold]Deva's Blessing[/gold] and [gold]Touch of Midas[/gold].", names, null);

        Assert.Equal(
            "Enemies have [url=zts:Deva%27s%20Blessing][gold]Deva's Blessing[/gold][/url] and [url=zts:Touch%20of%20Midas][gold]Touch of Midas[/gold][/url].",
            text);
        Assert.Equal(names, NestedTooltipText.LinkedKeywords(text));
        Assert.Equal("Deva's Blessing", NestedTooltipText.KeywordOf("zts:Deva%27s%20Blessing"));
        Assert.Contains("[url=zts:Deva%27s%20Blessing][bgcolor=", NestedTooltipText.Highlight(text, "Deva's Blessing"));
        Assert.Equal(text, NestedTooltipText.Linkify(text, names, null));
    }

    [Fact]
    public void EveryGlossaryKeyword_RoundTripsThroughItsLink_WithASafeTag()
    {
        foreach (string name in ZoneTheSpire.Core.Biomes.BiomeRegistry.All.SelectMany(biome => biome.Keywords).Select(keyword => keyword.Name))
        {
            string text = NestedTooltipText.Linkify($"[gold]{name}[/gold]", new[] { name }, null);
            string tag = text.Substring(0, text.IndexOf(']') + 1);

            Assert.DoesNotContain("'", tag);
            Assert.DoesNotContain("\"", tag);
            Assert.DoesNotContain(" ", tag);
            Assert.Equal(new[] { name }, NestedTooltipText.LinkedKeywords(text));
        }
    }
}
