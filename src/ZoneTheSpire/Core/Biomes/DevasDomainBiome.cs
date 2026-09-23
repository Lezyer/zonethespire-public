using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// Deva's Domain: a zone of rebirth and Karma (see KarmaRules). Fights give every player Samsara (played cards lose Karma, which
/// can go negative and raise their cost) and every enemy Deva's Blessing (it lives on for one more action after dying); the shop
/// sells no cards but 6 relics and 8 potions; campfires add Meditate (give a card Chakra 2 or double its Chakra, and heal). One
/// Attack or Skill in every card reward inside the zone gets Chakra 1-3.
/// </summary>
public sealed class DevasDomainBiome : BiomeDefinition
{
    public const string DevasFightsEffectId = "devas_domain.fights";
    public const string DevasShopEffectId = "devas_domain.shop";
    public const string MeditateEffectId = "devas_domain.meditate";

    private static readonly IReadOnlyList<BiomeEffect> DevasEffects = new[]
    {
        new BiomeEffect(DevasFightsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(DevasShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(MeditateEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("devas.samsara", DevasText.SamsaraDescription),
        new ZoneKeyword("devas.devas_blessing", DevasText.DevasBlessingDescription),
        new ZoneKeyword("devas.chakra", DevasText.ChakraDescription),
        new ZoneKeyword("devas.karma", DevasText.KarmaDescription),
    };

    public override string Id => "devas_domain";

    public override string DisplayName => ModText.Get("zone.devas_domain.name");

    public override string ColorHex => "F291C4";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => DevasEffects;
}
