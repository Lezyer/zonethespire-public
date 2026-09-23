using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.ZoneRelics;

/// <summary>
/// A relic a zone's treasure chests can offer. <paramref name="Key"/> is saved with the run (the relics that already
/// appeared), so never rename it; it may not contain '@' or ';'. A relic can be listed for several biomes.
/// </summary>
public sealed record ZoneRelicDefinition(string Key, IReadOnlyList<string> BiomeIds);

/// <summary>Every zone relic and the biomes whose chests can offer it (engine-free; the Run layer maps keys to relic models).</summary>
public static class ZoneRelicCatalog
{
    public const string VerminSymbiontKey = "vermin_symbiont";
    public const string CrimsonToothKey = "crimson_tooth";
    public const string LargeFieldstoneKey = "large_fieldstone";
    public const string MarblePauldronsKey = "marble_pauldrons";
    public const string ReflectiveShardKey = "reflective_shard";
    public const string GoldenWishmakerKey = "golden_wishmaker";
    public const string FlutteringPhantasmKey = "fluttering_phantasm";
    public const string SalvageMachineKey = "salvage_machine";
    public const string PrismCloudKey = "prism_cloud";
    public const string SeedOfHatredKey = "seed_of_hatred";
    public const string ScrollOfChantsKey = "scroll_of_chants";
    public const string FrostheartKey = "frostheart";
    public const string SacrosanctFlailKey = "sacrosanct_flail";
    public const string BrewExtractorKey = "brew_extractor";

    /// <summary>
    /// The treasure node effect every biome with zone relics gets (added by ZoneEffects.EffectsFor, not declared per biome).
    /// The tooltip doesn't name the relics.
    /// </summary>
    public static BiomeEffect ChestRelicEffect { get; } = new(
        "zone.chest_relic",
        new[] { NodeKind.Treasure });

    public static IReadOnlyList<ZoneRelicDefinition> All { get; } = new[]
    {
        new ZoneRelicDefinition(VerminSymbiontKey, new[] { new InfestationBiome().Id }),
        new ZoneRelicDefinition(CrimsonToothKey, new[] { new BloodRainBiome().Id }),
        new ZoneRelicDefinition(LargeFieldstoneKey, new[] { new FerrosandBiome().Id }),
        new ZoneRelicDefinition(MarblePauldronsKey, new[] { new ForgottenEmpireBiome().Id }),
        new ZoneRelicDefinition(ReflectiveShardKey, new[] { new MirrorlandsBiome().Id }),
        new ZoneRelicDefinition(GoldenWishmakerKey, new[] { new HallsOfMidasBiome().Id }),
        new ZoneRelicDefinition(FlutteringPhantasmKey, new[] { new PhantasmalTombsBiome().Id }),
        new ZoneRelicDefinition(SalvageMachineKey, new[] { new ScrapyardBiome().Id }),
        new ZoneRelicDefinition(PrismCloudKey, new[] { new PrismaticStormBiome().Id }),
        new ZoneRelicDefinition(SeedOfHatredKey, new[] { new ShadowCorruptionBiome().Id }),
        new ZoneRelicDefinition(ScrollOfChantsKey, new[] { new DevasDomainBiome().Id }),
        new ZoneRelicDefinition(FrostheartKey, new[] { new HoarfrostBiome().Id }),
        new ZoneRelicDefinition(SacrosanctFlailKey, new[] { new HallowedBiome().Id }),
        new ZoneRelicDefinition(BrewExtractorKey, new[] { new FermentoryBiome().Id }),
    };

    /// <summary>Keys of the relics <paramref name="biomeId"/>'s chests can offer, in catalog order (none for most biomes).</summary>
    public static IReadOnlyList<string> KeysFor(string biomeId) =>
        All.Where(relic => relic.BiomeIds.Contains(biomeId, StringComparer.Ordinal)).Select(relic => relic.Key).ToList();
}
