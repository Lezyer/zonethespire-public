using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// Hoarfrost: a frozen zone that makes you wait for your best turn (see FrostRules). In its fights 2 cards in your hand freeze
/// every turn and can't be played until the enemies' turn begins; its card rewards and shop cards carry Biting Cold, which makes
/// the enemy it is applied to take more damage from every attack until it thaws; its campfires add Frostbind, which puts more
/// Biting Cold on a card of your choice.
/// </summary>
public sealed class HoarfrostBiome : BiomeDefinition
{
    public const string FrozenHandEffectId = "hoarfrost.fights";
    public const string FrostShopEffectId = "hoarfrost.shop";
    public const string FrostbindEffectId = "hoarfrost.frostbind";

    private static readonly IReadOnlyList<BiomeEffect> FrostEffects = new[]
    {
        new BiomeEffect(FrozenHandEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(FrostShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(FrostbindEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("frost.hoarfrost", FrostText.HoarfrostDescription),
        new ZoneKeyword("frost.frozen", FrostText.FrozenDescription),
        new ZoneKeyword("frost.biting_cold", FrostText.BitingColdDescription),
    };

    public override string Id => "hoarfrost";

    public override string DisplayName => ModText.Get("zone.hoarfrost.name");

    public override string ColorHex => "2E6F8E";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => FrostEffects;
}
