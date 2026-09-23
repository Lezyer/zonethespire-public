using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class ForgottenEmpireBiome : BiomeDefinition
{
    public const string StatuesEffectId = "forgotten_empire.statues";
    public const string MarbledShopEffectId = "forgotten_empire.marbled_shop";
    public const string SculptEffectId = "forgotten_empire.sculpt";

    private static readonly IReadOnlyList<BiomeEffect> ForgottenEmpireEffects = new[]
    {
        new BiomeEffect(StatuesEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(MarbledShopEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(SculptEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("forgotten_empire.forgotten_statue", ForgottenEmpireText.ForgottenStatueDescription),
        new ZoneKeyword("forgotten_empire.marbled", ForgottenEmpireText.MarbledDescription),
        new ZoneKeyword("forgotten_empire.polishing", ForgottenEmpireText.PolishingDescription),
        new ZoneKeyword("forgotten_empire.marbling", ForgottenEmpireText.MarblingDescription),
    };

    public override string Id => "forgotten_empire";

    public override string DisplayName => ModText.Get("zone.forgotten_empire.name");

    public override string ColorHex => "9A62D9";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => ForgottenEmpireEffects;
}
