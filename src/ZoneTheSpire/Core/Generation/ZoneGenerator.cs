using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Generation;

/// <summary>
/// Deterministic biome zone generation. Each act gets 2-3 zones, each of a different biome (and, through ZoneStore, a biome no other act of the run uses), and zones never overlap:
/// every eligible node belongs to at most one zone. Every collection that affects output is iterated in sorted
/// (row, col) order; hash sets are used for lookups only. Integer arithmetic only.
/// </summary>
public static class ZoneGenerator
{
    public static string StreamName(int actIndex) => $"zonethespire_zones_act_{actIndex + 1}";

    public static IReadOnlyList<Zone> Generate(
        MapGraph graph,
        ulong runSeed,
        int actIndex,
        IReadOnlyList<string> biomeIds,
        ZoneGenerationSettings? settings = null)
    {
        settings ??= ZoneGenerationSettings.Default;
        var zones = new List<Zone>();
        if (biomeIds.Count == 0)
        {
            return zones;
        }

        List<GridCoord> eligible = graph.Nodes
            .Where(node => node.Kind.IsZoneEligible() && node.Coord.Row >= settings.MinStartRowFor(actIndex))
            .Select(node => node.Coord)
            .ToList();
        if (eligible.Count < settings.MinZoneSize)
        {
            return zones;
        }

        var rng = ZoneRandom.ForStream(runSeed, StreamName(actIndex));
        List<string> biomes = Shuffled(biomeIds.Distinct(StringComparer.Ordinal).ToList(), rng);
        int zoneCount = Math.Min(rng.NextInt(settings.MinZonesPerAct, settings.MaxZonesPerAct + 1), biomes.Count);
        var covered = new HashSet<GridCoord>();
        var exhausted = new HashSet<GridCoord>();

        while (zones.Count < zoneCount)
        {
            List<GridCoord> seeds = eligible.Where(coord => !covered.Contains(coord) && !exhausted.Contains(coord)).ToList();
            if (seeds.Count == 0)
            {
                break;
            }

            GridCoord seed = seeds[rng.NextInt(seeds.Count)];

            int size = rng.NextInt(settings.MinZoneSize, settings.MaxZoneSize + 1);

            List<GridCoord> cluster = Grow(seed, size, eligible, covered, rng);
            if (cluster.Count < settings.MinZoneSize)
            {
                exhausted.Add(seed);
                continue;
            }

            foreach (GridCoord coord in cluster)
            {
                covered.Add(coord);
            }

            // One biome per zone from the shuffled list, so biomes never repeat within an act.
            zones.Add(new Zone($"act{actIndex}-zone{zones.Count}", biomes[zones.Count], cluster));
        }

        return zones;
    }

    /// <summary>A single zone of <paramref name="biomeId"/> covering every eligible node of the map (debug command).</summary>
    public static IReadOnlyList<Zone> WholeMap(MapGraph graph, int actIndex, string biomeId)
    {
        List<GridCoord> nodes = graph.Nodes
            .Where(node => node.Kind.IsZoneEligible())
            .Select(node => node.Coord)
            .ToList();
        return nodes.Count == 0 ? Array.Empty<Zone>() : new[] { new Zone($"act{actIndex}-zone0", biomeId, nodes) };
    }

    /// <summary>Random flood fill over adjacent nodes that no zone has claimed yet.</summary>
    private static List<GridCoord> Grow(
        GridCoord seed,
        int size,
        IReadOnlyList<GridCoord> eligibleSorted,
        IReadOnlySet<GridCoord> covered,
        ZoneRandom rng)
    {
        var cluster = new List<GridCoord> { seed };
        var selected = new HashSet<GridCoord> { seed };
        while (cluster.Count < size)
        {
            // eligibleSorted is in (row, col) order, so the frontier is too.
            List<GridCoord> frontier = eligibleSorted
                .Where(coord => !covered.Contains(coord) && !selected.Contains(coord) && cluster.Any(member => member.IsAdjacentTo(coord)))
                .ToList();
            if (frontier.Count == 0)
            {
                break;
            }

            GridCoord pick = frontier[rng.NextInt(frontier.Count)];
            cluster.Add(pick);
            selected.Add(pick);
        }

        return cluster;
    }

    private static List<string> Shuffled(List<string> items, ZoneRandom rng)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }
}
