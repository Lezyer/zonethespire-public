using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class HallsOfMidasBiome : BiomeDefinition
{
    public const string TouchOfMidasEffectId = "halls_of_midas.touch_of_midas";
    public const string GildedShopEffectId = "halls_of_midas.gilded_shop";
    public const string LuxuriousRestEffectId = "halls_of_midas.luxurious_rest";

    private static readonly IReadOnlyList<BiomeEffect> HallsOfMidasEffects = new[]
    {
        new BiomeEffect(TouchOfMidasEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(GildedShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(LuxuriousRestEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("halls_of_midas.touch_of_midas", HallsOfMidasText.TouchOfMidasDescription),
    };

    public override string Id => "halls_of_midas";

    public override string DisplayName => ModText.Get("zone.halls_of_midas.name");

    public override string ColorHex => "D4A017";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => HallsOfMidasEffects;
}
