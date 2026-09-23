using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Persistence;

public enum EnsureOutcome
{
    /// <summary>Saved zones for this act matched the map and were reused.</summary>
    Loaded,

    /// <summary>No zones existed for this act; they were generated.</summary>
    Generated,

    /// <summary>Saved data was unusable (changed map, corrupt JSON, unknown version); zones were regenerated.</summary>
    Regenerated,

    /// <summary>The act's zones were overwritten by the <c>zone_map</c> debug command.</summary>
    Replaced,
}

public sealed record EnsureResult(string Json, IReadOnlyList<Zone> Zones, EnsureOutcome Outcome, uint Digest);

/// <summary>Load-or-generate logic for one act (spec §6.1). Persisted zones always win when the map matches.</summary>
public static class ZoneStore
{
    public static EnsureResult Ensure(
        string? existingJson,
        MapGraph graph,
        ulong runSeed,
        int actIndex,
        IReadOnlyList<string> biomeIds,
        ZoneGenerationSettings? settings = null)
    {
        string fingerprint = MapFingerprint.Compute(graph);
        bool valid = ZoneSerializer.TryDeserialize(existingJson, out ZoneSetRecord set);

        if (valid && set.Acts.TryGetValue(actIndex, out ActZoneRecord? saved) && saved.MapFingerprint == fingerprint)
        {
            List<Zone> loaded = (saved.Zones ?? new()).Select(ZoneSerializer.ToZone).ToList();
            return new EnsureResult(existingJson ?? string.Empty, loaded, EnsureOutcome.Loaded, ZoneDigest.Compute(actIndex, loaded));
        }

        bool hadUnusableData = !valid || set.Acts.ContainsKey(actIndex);
        if (!valid)
        {
            set = new ZoneSetRecord();
        }

        IReadOnlyList<Zone> generated = ZoneGenerator.Generate(graph, runSeed, actIndex, UnusedBiomes(set, actIndex, biomeIds, settings), settings);
        set.Acts[actIndex] = new ActZoneRecord
        {
            MapFingerprint = fingerprint,
            Zones = generated.Select(ZoneSerializer.FromZone).ToList(),
        };

        return new EnsureResult(
            ZoneSerializer.Serialize(set),
            generated,
            hadUnusableData ? EnsureOutcome.Regenerated : EnsureOutcome.Generated,
            ZoneDigest.Compute(actIndex, generated));
    }

    /// <summary>
    /// Stores <paramref name="zones"/> as this act's zones (debug command), keeping other acts. The act keeps the map's
    /// fingerprint, so a later <see cref="Ensure"/> for the same map loads these zones instead of regenerating.
    /// </summary>
    public static EnsureResult Replace(string? existingJson, MapGraph graph, int actIndex, IReadOnlyList<Zone> zones)
    {
        if (!ZoneSerializer.TryDeserialize(existingJson, out ZoneSetRecord set))
        {
            set = new ZoneSetRecord();
        }

        set.Acts[actIndex] = new ActZoneRecord
        {
            MapFingerprint = MapFingerprint.Compute(graph),
            Zones = zones.Select(ZoneSerializer.FromZone).ToList(),
        };

        return new EnsureResult(ZoneSerializer.Serialize(set), zones, EnsureOutcome.Replaced, ZoneDigest.Compute(actIndex, zones));
    }

    /// <summary>
    /// Biomes an act may use: those no other act of the run already has, so every zone biome is unique per run (14 biomes, at
    /// most 3 zones in each of 3 acts). Falls back to every biome only when fewer than the minimum zone count remain (e.g. a
    /// 4th act). Reads the synced, saved zone data, so every peer gets the same list.
    /// </summary>
    public static IReadOnlyList<string> BiomesForAct(string? json, int actIndex, IReadOnlyList<string> biomeIds, ZoneGenerationSettings? settings = null)
    {
        ZoneSetRecord set = ZoneSerializer.TryDeserialize(json, out ZoneSetRecord parsed) ? parsed : new ZoneSetRecord();
        return UnusedBiomes(set, actIndex, biomeIds, settings);
    }

    private static IReadOnlyList<string> UnusedBiomes(ZoneSetRecord set, int actIndex, IReadOnlyList<string> biomeIds, ZoneGenerationSettings? settings)
    {
        settings ??= ZoneGenerationSettings.Default;
        var used = new HashSet<string>(
            set.Acts
                .Where(act => act.Key != actIndex)
                .SelectMany(act => act.Value.Zones ?? new())
                .Select(record => ZoneSerializer.ToZone(record).BiomeId),
            StringComparer.Ordinal);
        List<string> unused = biomeIds.Where(id => !used.Contains(id)).ToList();
        return unused.Count >= settings.MinZonesPerAct ? unused : biomeIds;
    }

    public static IReadOnlyList<Zone> Read(string? json, int actIndex)
    {
        if (!ZoneSerializer.TryDeserialize(json, out ZoneSetRecord set) || !set.Acts.TryGetValue(actIndex, out ActZoneRecord? act))
        {
            return Array.Empty<Zone>();
        }

        return (act.Zones ?? new()).Select(ZoneSerializer.ToZone).ToList();
    }
}
