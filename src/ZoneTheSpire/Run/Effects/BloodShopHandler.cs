using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Blood Rain shops: prices paid in HP. Behaviour lives in BloodShop and its patches.</summary>
internal sealed class BloodShopHandler : ZoneEffectHandler
{
    public override string EffectId => BloodRainBiome.BloodShopEffectId;
}
