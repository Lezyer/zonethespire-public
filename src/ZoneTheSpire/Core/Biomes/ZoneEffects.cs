using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Core.Biomes;

public static class ZoneEffects
{
    /// <summary>
    /// The biome's effects that affect this node kind, in declaration order, then the shared chest relic effect for treasure
    /// nodes of a biome that has zone relics.
    /// </summary>
    public static IReadOnlyList<BiomeEffect> EffectsFor(BiomeDefinition biome, NodeKind kind)
    {
        List<BiomeEffect> effects = biome.Effects.Where(effect => effect.Affects(kind)).ToList();
        if (ZoneRelicCatalog.ChestRelicEffect.Affects(kind) && ZoneRelicCatalog.KeysFor(biome.Id).Count > 0)
        {
            effects.Add(ZoneRelicCatalog.ChestRelicEffect);
        }

        return effects;
    }
}
