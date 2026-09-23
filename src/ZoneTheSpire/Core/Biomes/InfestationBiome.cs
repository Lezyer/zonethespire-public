using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class InfestationBiome : BiomeDefinition
{
    public const string WrigglersEffectId = "infestation.wrigglers";
    public const string ShopWrigglingEffectId = "infestation.shop_wriggling";
    public const string FesteringSmithEffectId = "infestation.festering_smith";

    private static readonly IReadOnlyList<BiomeEffect> InfestationEffects = new[]
    {
        new BiomeEffect(WrigglersEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(ShopWrigglingEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(FesteringSmithEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("infestation.wriggling", InfestationText.WrigglingDescription),
        new ZoneKeyword("infestation.wriggler", InfestationText.WrigglerDescription),
    };

    public override string Id => "infestation";

    public override string DisplayName => ModText.Get("zone.infestation.name");

    public override string ColorHex => "2E6B2E";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => InfestationEffects;
}
