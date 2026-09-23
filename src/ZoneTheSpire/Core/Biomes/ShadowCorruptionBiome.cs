using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using System.Linq;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// Shadow Corruption: a harsh zone where you can't see clearly. Its nodes are hidden on the map until the party stands next to
/// them; fights hide enemy intents, shade a card in hand every turn and turn enemy attacks into Doom; the shop hides its stock at a
/// discount; campfires may add a curse when you rest. Leaving the zone pays out a reward per node crossed (see ShadowRules),
/// which every node tooltip mentions. One card in every card reward inside the zone, and 3 shop cards, are Shadow Corrupted
/// (doubled values, Doom when played; see ShadowCorruptionRules).
/// </summary>
public sealed class ShadowCorruptionBiome : BiomeDefinition
{
    public const string ShadowFightsEffectId = "shadow_corruption.fights";
    public const string ShadowShopEffectId = "shadow_corruption.shop";
    public const string TroubledDreamsEffectId = "shadow_corruption.troubled_dreams";

    private static readonly IReadOnlyList<BiomeEffect> ShadowEffects = new[]
    {
        new BiomeEffect(ShadowFightsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(ShadowShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(TroubledDreamsEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("shadow.shadow_brutality", ShadowText.ShadowBrutalityDescription),
        new ZoneKeyword("shadow.shaded", ShadowText.ShadedDescription),
        new ZoneKeyword("shadow.light_the_way", ShadowText.LightTheWayDescription),
        new ZoneKeyword("shadow.shadow_corrupted", ShadowText.ShadowCorruptedDescription),
    };

    public override string Id => "shadow_corruption";

    public override string DisplayName => ModText.Get("zone.shadow_corruption.name");

    public override string ColorHex => "3B2352";

    /// <summary>The map colour is too dark to read on the tooltip background.</summary>
    public override string? TooltipColorHex => "9A82C2";

    public override string? TooltipFooter => ModText.Get("zone.shadow_corruption.footer");

    /// <summary>Its mechanics, then the node kinds a hidden node's tip links to (rebuilt on every access, from the effects above).</summary>
    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords.Concat(ZoneTooltip.NodeKindKeywords(this)).ToList();

    public override IReadOnlyList<BiomeEffect> Effects => ShadowEffects;
}
