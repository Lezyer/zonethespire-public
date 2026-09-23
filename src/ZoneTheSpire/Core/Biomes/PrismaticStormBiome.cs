using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class PrismaticStormBiome : BiomeDefinition
{
    public const string StormBuffsEffectId = "prismatic_storm.storm_buffs";
    public const string TransformServiceEffectId = "prismatic_storm.transform_service";
    public const string StareAtPrismEffectId = "prismatic_storm.stare_at_prism";

    private static readonly IReadOnlyList<BiomeEffect> PrismaticEffects = new[]
    {
        new BiomeEffect(StormBuffsEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(TransformServiceEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(StareAtPrismEffectId,
            new[] { NodeKind.RestSite }),
    };

    public override string Id => "prismatic_storm";

    public override string DisplayName => ModText.Get("zone.prismatic_storm.name");

    public override string ColorHex => "F4F4F4";

    public override IReadOnlyList<BiomeEffect> Effects => PrismaticEffects;
}
