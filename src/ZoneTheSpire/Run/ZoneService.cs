using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.Persistence;

namespace ZoneTheSpire.Run;

/// <summary>
/// The single query API for zones (rendering now, mechanics later). Mechanics must only read zones through this
/// service and must only use the game's synchronized systems/RNG (spec §7.4 rule 8).
/// </summary>
public static class ZoneService
{
    private static ZoneTheSpireModifier? _cacheOwner;
    private static int _cacheVersion = -1;
    private static int _cacheAct = -1;
    private static IReadOnlyList<Zone> _cacheZones = Array.Empty<Zone>();

    /// <summary>Between runs (RunLifecycle): drops the cached zones and the reference to the last run's modifier.</summary>
    internal static void Reset()
    {
        _cacheOwner = null;
        _cacheVersion = -1;
        _cacheAct = -1;
        _cacheZones = Array.Empty<Zone>();
    }

    public static ZoneTheSpireModifier? FindModifier(IRunState runState) =>
        runState.Modifiers.OfType<ZoneTheSpireModifier>().FirstOrDefault();

    /// <summary>Called from the modifier's AfterMapGenerated on every peer. Loads saved zones or generates them.</summary>
    public static void EnsureZones(IRunState runState, ActMap map, int actIndex)
    {
        ZoneTheSpireModifier? modifier = FindModifier(runState);
        if (modifier == null)
        {
            return;
        }

        EnsureResult result = ZoneStore.Ensure(
            modifier.ZonesJson,
            MapGraphBuilder.Build(map),
            runState.Rng.Seed,
            actIndex,
            BiomeRegistry.Ids);

        if (!string.Equals(result.Json, modifier.ZonesJson, StringComparison.Ordinal))
        {
            modifier.ZonesJson = result.Json;
        }

        int coveredNodes = result.Zones.Sum(zone => zone.Nodes.Count);
        Log.Info($"Act {actIndex + 1}: {result.Zones.Count} zones covering {coveredNodes} node slots, digest={result.Digest:x8} ({result.Outcome}).");
    }

    /// <summary>
    /// Debug: overwrites the current act's zones with one zone of <paramref name="biomeId"/> covering the whole map, or,
    /// when it is null, with freshly generated zones. Deterministic, so every peer running the networked command agrees.
    /// Returns the new zones, or null when there is no run map.
    /// </summary>
    public static IReadOnlyList<Zone>? ReplaceCurrentActZones(IRunState runState, string? biomeId)
    {
        ZoneTheSpireModifier? modifier = FindModifier(runState);
        if (modifier == null)
        {
            return null;
        }

        MapGraph graph = MapGraphBuilder.Build(runState.Map);
        int actIndex = runState.CurrentActIndex;
        IReadOnlyList<Zone> zones = biomeId == null
            ? ZoneGenerator.Generate(graph, runState.Rng.Seed, actIndex, ZoneStore.BiomesForAct(modifier.ZonesJson, actIndex, BiomeRegistry.Ids))
            : ZoneGenerator.WholeMap(graph, actIndex, biomeId);

        EnsureResult result = ZoneStore.Replace(modifier.ZonesJson, graph, actIndex, zones);
        modifier.ZonesJson = result.Json;
        Log.Info($"Act {actIndex + 1}: zones replaced by debug command ({(biomeId ?? "random")}): {zones.Count} zones covering {zones.Sum(zone => zone.Nodes.Count)} nodes, digest={result.Digest:x8}.");
        return zones;
    }

    public static IReadOnlyList<Zone> GetZones(IRunState runState, int actIndex)
    {
        ZoneTheSpireModifier? modifier = FindModifier(runState);
        if (modifier == null)
        {
            return Array.Empty<Zone>();
        }

        if (!ReferenceEquals(modifier, _cacheOwner) || modifier.ZonesJsonVersion != _cacheVersion || actIndex != _cacheAct)
        {
            _cacheZones = ZoneStore.Read(modifier.ZonesJson, actIndex);
            _cacheOwner = modifier;
            _cacheVersion = modifier.ZonesJsonVersion;
            _cacheAct = actIndex;
        }

        return _cacheZones;
    }

    /// <summary>
    /// The zone that contains this node, or null. Zones never overlap, so a node belongs to at most one zone, and
    /// <see cref="Zone.Nodes"/> is the exact member list. This — not the drawn blob — is the source of truth for mechanics.
    /// </summary>
    public static Zone? GetZoneAt(IRunState runState, int actIndex, MapCoord coord)
    {
        var target = new GridCoord(coord.col, coord.row);
        return GetZones(runState, actIndex).FirstOrDefault(zone => zone.Nodes.Contains(target));
    }

    /// <summary>True when the node is a member of any zone in the given act.</summary>
    public static bool IsInZone(IRunState runState, int actIndex, MapCoord coord) =>
        GetZoneAt(runState, actIndex, coord) != null;

    public static Zone? GetCurrentZone(IRunState runState) =>
        runState.CurrentMapCoord is { } coord ? GetZoneAt(runState, runState.CurrentActIndex, coord) : null;
}
