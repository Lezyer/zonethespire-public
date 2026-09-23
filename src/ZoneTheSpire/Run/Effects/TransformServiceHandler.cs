using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Prismatic Storm shops: the card removal service transforms instead. Behaviour lives in TransformShop and its patches.</summary>
internal sealed class TransformServiceHandler : ZoneEffectHandler
{
    public override string EffectId => PrismaticStormBiome.TransformServiceEffectId;
}
