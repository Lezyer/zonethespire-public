using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// One effect of a biome. An effect targets a set of node kinds, so a zone can affect several node types, and a biome
/// may declare several effects (with overlapping or disjoint kinds). Behaviour lives in a game-side handler keyed by Id.
/// </summary>
public sealed class BiomeEffect
{
    /// <summary>An effect whose tooltip line is the text key <c>effect.&lt;id&gt;</c> (zone_the_spire table).</summary>
    public BiomeEffect(string id, IEnumerable<NodeKind> affectedKinds)
        : this(id, affectedKinds, null)
    {
    }

    /// <summary>An effect with a fixed tooltip line (tests).</summary>
    public BiomeEffect(string id, IEnumerable<NodeKind> affectedKinds, string? tooltipLine)
    {
        Id = id;
        AffectedKinds = new HashSet<NodeKind>(affectedKinds);
        _tooltipLine = tooltipLine;
    }

    private readonly string? _tooltipLine;

    /// <summary>Stable id, e.g. "mirrorlands.mirrored_foes". Never rename: handlers are registered by it.</summary>
    public string Id { get; }

    public IReadOnlySet<NodeKind> AffectedKinds { get; }

    /// <summary>Tooltip sentence; may contain {Zone} and other {Token} placeholders (see ZoneTooltip).</summary>
    public string TooltipLine => _tooltipLine ?? ModText.Get("effect." + Id);

    public bool Affects(NodeKind kind) => AffectedKinds.Contains(kind);
}
