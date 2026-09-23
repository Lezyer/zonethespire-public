using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class ScrapyardBiome : BiomeDefinition
{
    public const string ScrapBotsEffectId = "scrapyard.scrap_bots";
    public const string ScrapServiceEffectId = "scrapyard.scrap_service";
    public const string RummageEffectId = "scrapyard.rummage";

    private static readonly IReadOnlyList<BiomeEffect> ScrapyardEffects = new[]
    {
        new BiomeEffect(ScrapBotsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(ScrapServiceEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(RummageEffectId,
            new[] { NodeKind.RestSite }),
    };

    public override string Id => "scrapyard";

    public override string DisplayName => ModText.Get("zone.scrapyard.name");

    public override string ColorHex => "8B5A2B";

    public override IReadOnlyList<BiomeEffect> Effects => ScrapyardEffects;
}
