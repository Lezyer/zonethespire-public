using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Tooltips;

/// <summary>
/// Text side of nested tooltips: keywords written as [gold]Name[/gold] become [url=zts:Name] links that the hover tip label
/// reports when hovered (see Rendering/NestedTooltips). The name is percent-encoded in the tag: BBCode reads a quote in a
/// tag value as the start of a quoted value, so "Deva's Blessing" would swallow the rest of the text.
/// </summary>
public static class NestedTooltipText
{
    public const string LinkPrefix = "zts:";

    /// <summary>The opening link tag for a keyword.</summary>
    private static string OpenTag(string name) => $"[url={LinkPrefix}{Encode(name)}]";

    /// <summary>Percent-encodes everything but letters and digits, so no quote, space or bracket reaches the tag.</summary>
    private static string Encode(string name) =>
        string.Concat(name.Select(c => char.IsAsciiLetterOrDigit(c) ? c.ToString() : Uri.HexEscape(c)));

    /// <summary>Wraps every [gold]Name[/gold] whose Name is linkable (and not <paramref name="exclude"/>, the tip's own title) in a link.</summary>
    public static string Linkify(string text, IEnumerable<string> linkable, string? exclude)
    {
        foreach (string name in linkable.Where(name => name != exclude))
        {
            string gold = $"[gold]{name}[/gold]";
            string link = $"{OpenTag(name)}{gold}[/url]";
            text = text.Replace(link, gold, StringComparison.Ordinal).Replace(gold, link, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>The keyword a link's meta names, or null when the meta isn't one of ours.</summary>
    public static string? KeywordOf(string meta) =>
        meta.StartsWith(LinkPrefix, StringComparison.Ordinal) && meta.Length > LinkPrefix.Length
            ? Uri.UnescapeDataString(meta.Substring(LinkPrefix.Length))
            : null;

    /// <summary>Whether a tip is about one of the mod's keywords: its title is one, or its text names one in [gold].</summary>
    public static bool Mentions(string? title, string description, IEnumerable<string> keywords) =>
        keywords.Any(name => title == name || description.Contains($"[gold]{name}[/gold]", StringComparison.Ordinal));

    /// <summary>The keywords a linkified text links to, in reading order, each once.</summary>
    public static IReadOnlyList<string> LinkedKeywords(string text)
    {
        var found = new List<string>();
        string open = "[url=" + LinkPrefix;
        for (int start = text.IndexOf(open, StringComparison.Ordinal); start >= 0; start = text.IndexOf(open, start + 1, StringComparison.Ordinal))
        {
            int end = text.IndexOf(']', start);
            if (end < 0)
            {
                break;
            }

            string name = Uri.UnescapeDataString(text.Substring(start + open.Length, end - start - open.Length));
            if (name.Length > 0 && !found.Contains(name))
            {
                found.Add(name);
            }
        }

        return found;
    }

    /// <summary>Shows a keyword's first link as selected (controller navigation): white on a gold background.</summary>
    public static string Highlight(string text, string? keyword)
    {
        if (keyword == null)
        {
            return text;
        }

        string link = $"{OpenTag(keyword)}[gold]{keyword}[/gold][/url]";
        int index = text.IndexOf(link, StringComparison.Ordinal);
        return index < 0
            ? text
            : text.Substring(0, index) + $"{OpenTag(keyword)}[bgcolor=#F2D14B66][color=#FFFFFF]{keyword}[/color][/bgcolor][/url]" + text.Substring(index + link.Length);
    }

    /// <summary>The index <paramref name="step"/> places from <paramref name="index"/>, wrapping around; -1 when there is nothing.</summary>
    public static int Cycle(int index, int step, int count) => count <= 0 ? -1 : ((index + step) % count + count) % count;
}
