using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Mirrorlands shops: the card removal service duplicates instead. Behaviour lives in MirrorShop and its patches.</summary>
internal sealed class DuplicateServiceHandler : ZoneEffectHandler
{
    public override string EffectId => MirrorlandsBiome.DuplicateServiceEffectId;
}
