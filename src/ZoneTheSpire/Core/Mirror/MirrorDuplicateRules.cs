using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Mirror;

/// <summary>Pure rules for the Mirrorlands 1-of-3 card duplicate choice. Uses the mod's own RNG so every peer agrees.</summary>
public static class MirrorDuplicateRules
{
    /// <summary>How many random cards Mirrored Rest and Mirrored Mend offer to duplicate.</summary>
    public const int OfferCount = 3;

    /// <summary>
    /// Stable key of a run location: act, map row and column (absent for debug rooms, written "-") and room id. The room
    /// id alone is not enough because the game restarts room ids at every map location.
    /// </summary>
    public static string LocationKey(int actIndex, int? mapRow, int? mapCol, int roomId) =>
        "a" + StableHash.Inv(actIndex)
            + ".r" + (mapRow is { } row ? StableHash.Inv(row) : "-")
            + ".c" + (mapCol is { } col ? StableHash.Inv(col) : "-")
            + ".i" + StableHash.Inv(roomId);

    /// <summary>
    /// Indices of the deck cards offered: <see cref="OfferCount"/> distinct indices (every card when the deck is smaller, none
    /// for an empty deck), derived from the run seed, location key (<see cref="LocationKey"/>), chooser and source player ids
    /// (no game RNG consumed).
    /// </summary>
    public static IReadOnlyList<int> PickOffer(ulong runSeed, string locationKey, ulong chooserId, ulong sourceId, int deckCount)
    {
        int count = Math.Min(OfferCount, Math.Max(0, deckCount));
        if (count == 0)
        {
            return Array.Empty<int>();
        }

        string stream = "mirrorlands.duplicate:" + locationKey
                        + ":" + chooserId.ToString(CultureInfo.InvariantCulture)
                        + ":" + sourceId.ToString(CultureInfo.InvariantCulture);
        ZoneRandom random = ZoneRandom.ForStream(runSeed, stream);
        var picks = new List<int>(count);
        while (picks.Count < count)
        {
            // The n-th pick is uniform over the indices not picked yet: skip past every earlier pick at or below it.
            int index = random.NextInt(deckCount - picks.Count);
            foreach (int picked in picks.OrderBy(p => p))
            {
                if (index >= picked)
                {
                    index++;
                }
            }

            picks.Add(index);
        }

        return picks;
    }
}
