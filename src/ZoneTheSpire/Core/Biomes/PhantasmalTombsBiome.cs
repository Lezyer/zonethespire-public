using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class PhantasmalTombsBiome : BiomeDefinition
{
    public const string PhantasmsEffectId = "phantasmal_tombs.phantasms";
    public const string RaidedShopEffectId = "phantasmal_tombs.raided_shop";
    public const string HauntEffectId = "phantasmal_tombs.haunt";

    private static readonly IReadOnlyList<BiomeEffect> PhantasmalTombsEffects = new[]
    {
        new BiomeEffect(PhantasmsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(RaidedShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(HauntEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("phantasmal.phantasm", PhantasmalText.PhantasmDescription),
        new ZoneKeyword("phantasmal.phantasm_haunted", PhantasmalText.PhantasmHauntedDescription),
    };

    public override string Id => "phantasmal_tombs";

    public override string DisplayName => ModText.Get("zone.phantasmal_tombs.name");

    public override string ColorHex => "6FD6A8";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => PhantasmalTombsEffects;
}
