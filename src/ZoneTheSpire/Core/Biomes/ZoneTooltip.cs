using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public static class ZoneTooltip
{
    private static readonly Regex TokenPattern = new(@"\{([A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// The hover-tip description for a node of this kind in this biome, or null when no effect applies. Lines of all
    /// applicable effects are joined with a space, and only the first keeps its "{Zone}: " prefix; the biome's footer (if any) follows on its own line, also for kinds with no
    /// effect. {Zone} becomes the zone name in [color=#outlineHex]; any other {Token} is replaced by resolveToken(Token), or kept
    /// as-is when the resolver returns null.
    /// </summary>
    public static string? BuildDescription(BiomeDefinition biome, NodeKind kind, string outlineHex, Func<string, string?> resolveToken)
    {
        IReadOnlyList<BiomeEffect> effects = ZoneEffects.EffectsFor(biome, kind);
        var lines = new List<string>();
        if (effects.Count > 0)
        {
            lines.Add(string.Join(" ", effects.Select((effect, index) => index == 0 ? effect.TooltipLine : Capitalized(WithoutZonePrefix(effect.TooltipLine)))));
        }

        if (biome.TooltipFooter is { } footer)
        {
            lines.Add(footer);
        }

        return lines.Count == 0 ? null : Resolve(biome, string.Join("\n", lines), outlineHex, resolveToken);
    }

    /// <summary>
    /// The hover-tip description for a hidden node (Shadow Corruption's Shadowed Path): the node's kind is unknown, so the tip names
    /// the kinds it could be, each a link to what that kind does in this biome (<see cref="NodeKindKeywords"/>), then the biome's
    /// footer (if any).
    /// </summary>
    public static string BuildHiddenDescription(BiomeDefinition biome, string outlineHex, Func<string, string?> resolveToken)
    {
        var lines = new List<string> { HiddenNodeLine };
        if (biome.TooltipFooter is { } footer)
        {
            lines.Add(footer);
        }

        return Resolve(biome, string.Join("\n", lines), outlineHex, resolveToken);
    }

    /// <summary>
    /// The biome's glossary keywords a tooltip text names as [gold]Name[/gold], in order of first mention, followed by the keywords
    /// those keywords' own descriptions name (breadth first), each once.
    /// </summary>
    public static IReadOnlyList<ZoneKeyword> KeywordsIn(BiomeDefinition biome, string text)
    {
        var found = new List<ZoneKeyword>();
        var queue = new Queue<string>();
        queue.Enqueue(text);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (ZoneKeyword keyword in biome.Keywords
                         .Select(keyword => (Keyword: keyword, Index: current.IndexOf("[gold]" + keyword.Name + "[/gold]", StringComparison.Ordinal)))
                         .Where(hit => hit.Index >= 0)
                         .OrderBy(hit => hit.Index)
                         .Select(hit => hit.Keyword))
            {
                if (!found.Contains(keyword))
                {
                    found.Add(keyword);
                    queue.Enqueue(keyword.Description);
                }
            }
        }

        return found;
    }

    private const string ZonePrefix = "{Zone}: ";

    /// <summary>Upper-cases the first letter outside [tags], so a line that loses its "{Zone}: " prefix still starts a sentence.</summary>
    private static string Capitalized(string line)
    {
        bool inTag = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '[')
            {
                inTag = true;
            }
            else if (c == ']')
            {
                inTag = false;
            }
            else if (!inTag && char.IsLetter(c))
            {
                return char.IsUpper(c) ? line : line.Substring(0, i) + char.ToUpperInvariant(c) + line.Substring(i + 1);
            }
        }

        return line;
    }

    private static string WithoutZonePrefix(string line) =>
        line.StartsWith(ZonePrefix, StringComparison.Ordinal) ? line.Substring(ZonePrefix.Length) : line;

    /// <summary>
    /// The node kinds a hidden node's tip names, as glossary keywords: the text key of each name (reserved names: no other tip
    /// may use them in [gold]).
    /// </summary>
    public static IReadOnlyList<(string Key, NodeKind Kind)> HiddenNodeKinds { get; } = new[]
    {
        ("hidden_node.fight", NodeKind.Monster),
        ("hidden_node.rest_site", NodeKind.RestSite),
        ("hidden_node.shop", NodeKind.Shop),
        ("hidden_node.unknown", NodeKind.Unknown),
        ("hidden_node.chest", NodeKind.Treasure),
    };

    /// <summary>First line of a hidden node's tip: every kind it could be, each a link.</summary>
    public static string HiddenNodeLine => ModText.Get("hidden_node.line");

    /// <summary>What each hidden node kind does in this biome, as glossary keywords (the links in <see cref="HiddenNodeLine"/>).</summary>
    public static IReadOnlyList<ZoneKeyword> NodeKindKeywords(BiomeDefinition biome) =>
        HiddenNodeKinds.Select(kind => new ZoneKeyword(kind.Key, NodeKindDescription(biome, kind.Kind))).ToList();

    /// <summary>The node kind's effect lines without the zone name; a "?" node only has them if it turns out to be a fight.</summary>
    public static string NodeKindDescription(BiomeDefinition biome, NodeKind kind)
    {
        IReadOnlyList<BiomeEffect> effects = ZoneEffects.EffectsFor(biome, kind);
        if (effects.Count == 0)
        {
            return ModText.Get("hidden_node.nothing");
        }

        return kind == NodeKind.Unknown
            ? ModText.Format("hidden_node.unknown_if_fight", ("Effects", string.Join(" ", effects.Select(effect => WithoutZonePrefix(effect.TooltipLine)))))
            : string.Join(" ", effects.Select(effect => Capitalized(WithoutZonePrefix(effect.TooltipLine))));
    }

    private static string Resolve(BiomeDefinition biome, string text, string outlineHex, Func<string, string?> resolveToken)
    {
        string zoneName = $"[color=#{outlineHex}]{biome.DisplayName}[/color]";
        return TokenPattern.Replace(text, match =>
        {
            string token = match.Groups[1].Value;
            return token == "Zone" ? zoneName : resolveToken(token) ?? match.Value;
        });
    }
}
