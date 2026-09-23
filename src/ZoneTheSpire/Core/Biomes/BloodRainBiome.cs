using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class BloodRainBiome : BiomeDefinition
{
    public const string BloodDrinkersEffectId = "blood_rain.blood_drinkers";
    public const string BloodSacrificeEffectId = "blood_rain.blood_sacrifice";
    public const string BloodShopEffectId = "blood_rain.blood_shop";

    private static readonly IReadOnlyList<BiomeEffect> BloodRainEffects = new[]
    {
        new BiomeEffect(BloodDrinkersEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(BloodSacrificeEffectId,
            new[] { NodeKind.RestSite }),
        new BiomeEffect(BloodShopEffectId,
            new[] { NodeKind.Shop }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("blood_rain.blood_drinker", BloodRainText.BloodDrinkerDescription),
    };

    public override string Id => "blood_rain";

    public override string DisplayName => ModText.Get("zone.blood_rain.name");

    public override string ColorHex => "7A0A1E";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => BloodRainEffects;
}
