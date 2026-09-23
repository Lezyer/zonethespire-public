using System;
using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.ZoneRelics;

/// <summary>
/// Engine-free rules for zone chest relics. A chest in a zone offers one extra relic 50% of the time, picked from the zone's
/// relics that haven't appeared in a chest yet. Rolls use the run seed and the chest's location (never a game RNG stream),
/// so every multiplayer peer offers the same relic.
/// <para>
/// Appeared relics are saved as "key@location" pairs. A relic that appeared at the chest being rolled still counts as
/// available there, so re-entering the same chest (e.g. after loading a save made in the room) offers the same relic again.
/// </para>
/// </summary>
public static class ZoneRelicRules
{
    public const int OfferChancePercent = 50;

    private const char PairSeparator = ';';
    private const char LocationSeparator = '@';

    /// <summary>
    /// Whether an actual treasure room can roll a zone relic given the map node it came from. A question-mark node that
    /// became a chest normally counts as a treasure node once entered; <see cref="NodeKind.Unknown"/> is still accepted as a fallback.
    /// The runtime calls this only from the treasure-room relic picker; it does not predict a question mark's outcome.
    /// </summary>
    public static bool ResolvedTreasureCanOffer(NodeKind originalMapKind) =>
        originalMapKind is NodeKind.Treasure or NodeKind.Unknown;

    /// <summary>Whether the chest at <paramref name="locationKey"/> rolls a zone relic offer (50%).</summary>
    public static bool RollsOffer(ulong runSeed, string locationKey) =>
        ZoneRandom.ForStream(runSeed, "zonerelic.offer:" + locationKey).NextInt(100) < OfferChancePercent;

    /// <summary>
    /// The relic key offered at <paramref name="locationKey"/>, or null. The 50% roll comes first (skipped when
    /// <paramref name="forceOffer"/>); then one of <paramref name="candidates"/> that hasn't appeared at another chest is
    /// picked, so a seen relic is never rolled and thrown away while an unseen one remains.
    /// </summary>
    public static string? PickOffer(
        ulong runSeed,
        string locationKey,
        IReadOnlyList<string> candidates,
        IReadOnlyDictionary<string, string> appeared,
        bool forceOffer = false)
    {
        if (!forceOffer && !RollsOffer(runSeed, locationKey))
        {
            return null;
        }

        List<string> available = candidates
            .Distinct(StringComparer.Ordinal)
            .Where(key => !appeared.TryGetValue(key, out string? at) || at == locationKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        return available.Count == 0
            ? null
            : available[ZoneRandom.ForStream(runSeed, "zonerelic.pick:" + locationKey).NextInt(available.Count)];
    }

    /// <summary>Reads the saved "key@location;key@location" list. Malformed pairs are skipped; the first location of a key wins.</summary>
    public static IReadOnlyDictionary<string, string> ParseAppeared(string? saved)
    {
        var appeared = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(saved))
        {
            return appeared;
        }

        foreach (string pair in saved.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            int at = pair.IndexOf(LocationSeparator);
            if (at > 0 && at < pair.Length - 1)
            {
                appeared.TryAdd(pair[..at], pair[(at + 1)..]);
            }
        }

        return appeared;
    }

    /// <summary>The saved list with <paramref name="key"/> recorded at <paramref name="locationKey"/> (unchanged if it already appeared).</summary>
    public static string MarkAppeared(string? saved, string key, string locationKey)
    {
        var appeared = new Dictionary<string, string>(ParseAppeared(saved), StringComparer.Ordinal);
        appeared.TryAdd(key, locationKey);
        return string.Join(
            PairSeparator,
            appeared.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Key + LocationSeparator + entry.Value));
    }
}
