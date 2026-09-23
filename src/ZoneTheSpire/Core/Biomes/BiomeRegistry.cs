using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// All biomes in a fixed order. The order is an input to deterministic generation: append new biomes at the end
/// (existing saves keep their stored zones; only newly generated acts see the new biome).
/// </summary>
public static class BiomeRegistry
{
    public static IReadOnlyList<BiomeDefinition> All { get; } = new BiomeDefinition[]
    {
        new ScrapyardBiome(),
        new MirrorlandsBiome(),
        new InfestationBiome(),
        new PrismaticStormBiome(),
        new BloodRainBiome(),
        new HallsOfMidasBiome(),
        new PhantasmalTombsBiome(),
        new FerrosandBiome(),
        new ForgottenEmpireBiome(),
        new ShadowCorruptionBiome(),
        new DevasDomainBiome(),
        new HoarfrostBiome(),
        new HallowedBiome(),
        new FermentoryBiome(),
    };

    public static IReadOnlyList<string> Ids { get; } = All.Select(biome => biome.Id).ToList();

    public static BiomeDefinition? Get(string id) =>
        All.FirstOrDefault(biome => string.Equals(biome.Id, id, StringComparison.Ordinal));
}
