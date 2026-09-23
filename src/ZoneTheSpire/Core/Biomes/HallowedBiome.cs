using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// Blinding Hallows: judgement waits at the end of the turn (see HallowedRules). Fights give players Blasphemer and enemies
/// Zealous; the shop hallows random attacks; campfires add Repent and replace Smith with Blaspheme. On the map it is a white
/// zone with a yellow border and a yellow name.
/// </summary>
public sealed class HallowedBiome : BiomeDefinition
{
    public const string FightsEffectId = "blinding_hallowed.fights";
    public const string ShopEffectId = "blinding_hallowed.shop";
    public const string RepentEffectId = "blinding_hallowed.repent";
    public const string BlasphemeEffectId = "blinding_hallowed.blaspheme";

    private static readonly IReadOnlyList<BiomeEffect> HallowedEffects = new[]
    {
        new BiomeEffect(FightsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(ShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(RepentEffectId,
            new[] { NodeKind.RestSite }),
        new BiomeEffect(BlasphemeEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> HallowedKeywords => new[]
    {
        new ZoneKeyword("hallowed.blasphemer", HallowedText.BlasphemerDescription),
        new ZoneKeyword("hallowed.blasphemous", HallowedText.BlasphemousDescription),
        new ZoneKeyword("hallowed.zealous", HallowedText.ZealousDescription),
        new ZoneKeyword("hallowed.hallowing", HallowedText.HallowingDescription),
        new ZoneKeyword("hallowed.redemption", HallowedText.RedemptionDescription),
        new ZoneKeyword("hallowed.hallowed", HallowedText.HallowedDescription),
    };

    public override string Id => "blinding_hallowed";

    public override string DisplayName => ModText.Get("zone.blinding_hallowed.name");

    public override string ColorHex => "F2D14B";

    /// <summary>White fill on the map; the border and the zone name keep the yellow ColorHex.</summary>
    public override string? MapFillColorHex => "FFFFFF";

    public override IReadOnlyList<ZoneKeyword> Keywords => HallowedKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => HallowedEffects;
}
