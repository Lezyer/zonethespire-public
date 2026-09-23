using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>
/// Engine-free description used by the deterministic zone-event picker. A single event may list several biomes, and
/// several events may list the same biome.
/// </summary>
public sealed record ZoneEventCandidate(string Key, IReadOnlyList<string> BiomeIds);

/// <summary>
/// Deterministic selection rules for zone events. The roll uses an independent named stream derived from the run seed
/// and map location, so it never advances a vanilla RNG stream and produces the same result on every multiplayer peer.
/// </summary>
public static class ZoneEventRules
{
    public const int DefaultOfferChancePercent = 20;
    public const int MissIncreasePercent = 10;
    public const int MaxOfferChancePercent = 60;
    public const int AfterZoneEventChancePercent = 0;

    /// <summary>Zone events only replace real events rolled from question-mark nodes.</summary>
    public static bool ResolvedEventCanRoll(NodeKind originalMapKind) => originalMapKind == NodeKind.Unknown;

    /// <summary>A stable key for one map node. Act is included so identical coordinates in later acts roll independently.</summary>
    public static string LocationKey(int actIndex, int row, int col) =>
        actIndex.ToString(CultureInfo.InvariantCulture) + ":" +
        row.ToString(CultureInfo.InvariantCulture) + ":" +
        col.ToString(CultureInfo.InvariantCulture);

    /// <summary>Constrains persisted or migrated chance values to the supported run-wide range.</summary>
    public static int NormalizeOfferChance(int chancePercent) =>
        Math.Clamp(chancePercent, AfterZoneEventChancePercent, MaxOfferChancePercent);

    /// <summary>
    /// The next run-wide chance after resolving an event room in a zone. A zone event resets it to zero; every other
    /// event adds ten percentage points up to sixty.
    /// </summary>
    public static int NextOfferChance(int currentChancePercent, bool wasZoneEvent) =>
        wasZoneEvent
            ? AfterZoneEventChancePercent
            : Math.Min(MaxOfferChancePercent, NormalizeOfferChance(currentChancePercent) + MissIncreasePercent);

    public static bool RollsZoneEvent(ulong runSeed, string locationKey, int chancePercent) =>
        ZoneRandom.ForStream(runSeed, "zoneevent.offer:" + locationKey).NextInt(100) < NormalizeOfferChance(chancePercent);

    public static bool RollsZoneEvent(ulong runSeed, string locationKey) =>
        RollsZoneEvent(runSeed, locationKey, DefaultOfferChancePercent);

    /// <summary>
    /// Picks an unvisited event for <paramref name="biomeId"/>, or null. Candidate keys are normalized into ordinal
    /// order before selection, so registration and reflection order cannot affect multiplayer results.
    /// </summary>
    public static string? PickEvent(
        ulong runSeed,
        string locationKey,
        string biomeId,
        IEnumerable<ZoneEventCandidate> candidates,
        IReadOnlySet<string> visitedKeys,
        bool forceRoll = false,
        int offerChancePercent = DefaultOfferChancePercent)
    {
        List<string> available = candidates
            .Where(candidate => candidate.BiomeIds.Contains(biomeId, StringComparer.Ordinal))
            .Select(candidate => candidate.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key) && !visitedKeys.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (available.Count == 0 || (!forceRoll && !RollsZoneEvent(runSeed, locationKey, offerChancePercent)))
        {
            return null;
        }

        int index = ZoneRandom.ForStream(runSeed, "zoneevent.pick:" + locationKey).NextInt(available.Count);
        return available[index];
    }
}
