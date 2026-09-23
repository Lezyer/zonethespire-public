using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// The Fermentory: a potion manufactory (see FermentoryRules). Enemies brew potions and fights give an extra potion-themed
/// reward; the shop sells 6 potions and a Bottle (+1 potion slot) instead of card removal; campfires add Distil, which makes a
/// potion slot special. On the map it is a dark brown zone with a dark green border and name.
/// </summary>
public sealed class FermentoryBiome : BiomeDefinition
{
    public const string FightsEffectId = "fermentory.fights";
    public const string ShopEffectId = "fermentory.shop";
    public const string DistilEffectId = "fermentory.distil";

    private static readonly IReadOnlyList<BiomeEffect> FermentoryEffects = new[]
    {
        new BiomeEffect(FightsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(ShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(DistilEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> FermentoryKeywords => new[]
    {
        new ZoneKeyword("fermentory.brewing", FermentoryText.BrewingDescription),
        new ZoneKeyword("fermentory.special_slot", FermentoryText.SpecialSlotDescription),
        new ZoneKeyword("fermentory.bottle", FermentoryText.BottleDescription),
        new ZoneKeyword("fermentory.distil", FermentoryText.DistilDescription),
    };

    public override string Id => "fermentory";

    public override string DisplayName => ModText.Get("zone.fermentory.name");

    public override string ColorHex => "1F4A2A";

    /// <summary>Dark brown fill on the map; the border and the zone name keep the dark green ColorHex.</summary>
    public override string? MapFillColorHex => "3B2414";

    public override IReadOnlyList<ZoneKeyword> Keywords => FermentoryKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => FermentoryEffects;
}
