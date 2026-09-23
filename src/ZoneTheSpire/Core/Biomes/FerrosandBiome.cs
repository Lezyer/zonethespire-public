using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class FerrosandBiome : BiomeDefinition
{
    public const string MagneticFoesEffectId = "ferrosand.magnetic_foes";
    public const string MagneticShopEffectId = "ferrosand.magnetic_shop";
    public const string MagnetizeEffectId = "ferrosand.magnetize";

    private static readonly IReadOnlyList<BiomeEffect> FerrosandEffects = new[]
    {
        new BiomeEffect(MagneticFoesEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(MagneticShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(MagnetizeEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("ferrosand.magnetized", FerrosandText.MagnetizedDescription),
        new ZoneKeyword("ferrosand.ferroform", FerrosandText.FerroformDescription),
        new ZoneKeyword("ferrosand.magnetic", FerrosandText.MagneticDescription),
    };

    public override string Id => "ferrosand";

    public override string DisplayName => ModText.Get("zone.ferrosand.name");

    public override string ColorHex => "D9BF8F";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => FerrosandEffects;
}
