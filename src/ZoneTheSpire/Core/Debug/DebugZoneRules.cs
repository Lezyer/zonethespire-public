using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Core.Debug;

/// <summary>The kind of fight a debug encounter is (mirrors the game's combat room types).</summary>
public enum DebugEncounterKind
{
    Monster,
    Elite,
    Boss,
}

/// <summary>Non-combat rooms the <c>room_with_zone</c> debug command can enter.</summary>
public enum DebugRoomKind
{
    Shop,
    RestSite,
    Treasure,
}

/// <summary>Pure rules for the zone debug commands (<c>fight_with_zone</c>, <c>room_with_zone</c> and the others).</summary>
public static class DebugZoneRules
{
    public static string ZoneIdList { get; } = string.Join(", ", BiomeRegistry.Ids);

    /// <summary>The biome whose id matches <paramref name="input"/> ignoring case and surrounding whitespace, or null.</summary>
    public static BiomeDefinition? ResolveBiome(string input)
    {
        string id = input.Trim();
        return id.Length == 0
            ? null
            : BiomeRegistry.All.FirstOrDefault(biome => string.Equals(biome.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The zone node kind a debug fight counts as. Bosses aren't a zone node kind, so they use Elite effects.</summary>
    public static NodeKind NodeKindFor(DebugEncounterKind encounter) =>
        encounter == DebugEncounterKind.Monster ? NodeKind.Monster : NodeKind.Elite;

    /// <summary><c>zone_map</c> argument that regenerates random zones instead of filling the map with one biome.</summary>
    public const string RandomZonesArg = "random";

    /// <summary>Every <c>zone_map</c> argument: each biome id, then <see cref="RandomZonesArg"/>.</summary>
    public static IReadOnlyList<string> ZoneMapArgs { get; } = BiomeRegistry.Ids.Append(RandomZonesArg).ToList();

    /// <summary>True when a <c>zone_map</c> argument asks for random zones (ignoring case and surrounding whitespace).</summary>
    public static bool IsRandomZonesArg(string input) =>
        string.Equals(input.Trim(), RandomZonesArg, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> RoomIds { get; } = new[] { "shop", "rest", "treasure" };

    public static string RoomIdList { get; } = string.Join(", ", RoomIds);

    /// <summary>
    /// "shop"/"merchant" → Shop, "rest"/"campfire"/"rest_site"/"restsite" → RestSite, "treasure"/"chest" → Treasure (ignoring case and
    /// whitespace).
    /// </summary>
    public static DebugRoomKind? ResolveRoom(string input) => input.Trim().ToLowerInvariant() switch
    {
        "shop" or "merchant" => DebugRoomKind.Shop,
        "rest" or "campfire" or "rest_site" or "restsite" => DebugRoomKind.RestSite,
        "treasure" or "chest" => DebugRoomKind.Treasure,
        _ => null,
    };

    /// <summary>
    /// <c>event_with_zone</c>: always picks one of the zone's events (no chance roll), preferring ones not seen this run; when
    /// all were seen, any of them. Null only when the zone has no events. Seeded like a real roll, so every peer picks the same.
    /// </summary>
    public static string? PickForcedEvent(
        ulong runSeed,
        string attemptKey,
        string biomeId,
        IReadOnlyList<ZoneEventCandidate> candidates,
        IReadOnlySet<string> visitedKeys) =>
        ZoneEventRules.PickEvent(runSeed, attemptKey, biomeId, candidates, visitedKeys, forceRoll: true)
        ?? ZoneEventRules.PickEvent(runSeed, attemptKey, biomeId, candidates, new HashSet<string>(), forceRoll: true);

    /// <summary>
    /// <c>event_with_zone_random</c>: any zone event with equal chance, seen or not, and one of its zones (a shared event picks
    /// either of its zones evenly). Null only when there are no events. Seeded, so every peer picks the same.
    /// </summary>
    public static (string Key, string BiomeId)? PickAnyEvent(ulong runSeed, string attemptKey, IReadOnlyList<ZoneEventCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        ZoneRandom random = ZoneRandom.ForStream(runSeed, "debug-random-event:" + attemptKey);
        ZoneEventCandidate picked = candidates[random.NextInt(candidates.Count)];
        return picked.BiomeIds.Count == 0 ? null : (picked.Key, picked.BiomeIds[random.NextInt(picked.BiomeIds.Count)]);
    }

    public static NodeKind NodeKindFor(DebugRoomKind room) => room switch
    {
        DebugRoomKind.Shop => NodeKind.Shop,
        DebugRoomKind.Treasure => NodeKind.Treasure,
        _ => NodeKind.RestSite,
    };
}
