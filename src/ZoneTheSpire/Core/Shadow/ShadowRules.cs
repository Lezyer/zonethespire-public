using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Shadow;

/// <summary>
/// Engine-free Shadow Corruption rules: Shadow Brutality, Light the Way, shaded cards, the shrouded shop, the Shadowed Path
/// reveal, Troubled Dreams and the Dawn payout. Random picks use the run seed and synced state (never a game RNG stream), so
/// every multiplayer peer gets the same results.
/// </summary>
public static class ShadowRules
{
    public const int BrutalityDoomPercent = 50;
    public const int LightTheWayCopies = 5;
    public const int LightTheWayDoomRemoved = 10;
    public const int LanternLightCopies = 5;
    public const int LanternLightCost = 1;
    public const int LanternLightUpgradedCost = 0;
    public const int LanternLightHeal = 3;
    public const int LanternLightStrength = 1;
    public const int LanternLightDexterity = 1;
    public const int LanternLightDoomRemoved = 5;
    public const int TroubledDreamsCursePercent = 50;
    public const int ShopShadedCards = 4;
    public const int ShopDiscountPercent = 60;
    public const int DawnGoldPerNode = 25;
    public const int DawnHealPercentPerNode = 10;
    public const int DawnRelicMinNodes = 3;

    /// <summary>
    /// Shadow Brutality: Doom a player gets from an enemy's attack: 50% of its full damage (before Block and anything else absorbs
    /// it), rounded down, at least 1. Non-attack damage gives none.
    /// </summary>
    public static int BrutalityDoom(decimal damage, bool poweredAttack) =>
        poweredAttack && damage > 0 ? Math.Max(1, (int)Math.Floor(damage * BrutalityDoomPercent / 100m)) : 0;

    /// <summary>Doom left after Light the Way removes 10 (never below 0).</summary>
    public static int DoomAfterLight(int doom) => DoomAfterRemoving(doom, LightTheWayDoomRemoved);

    /// <summary>Doom left after removing <paramref name="removed"/> (never below 0).</summary>
    public static int DoomAfterRemoving(int doom, int removed) => Math.Max(0, doom - Math.Max(0, removed));

    /// <summary>
    /// Last-Light Lantern (The Lantern Bearer event): its holder can't see enemy intents in any fight, exactly as in a Shadow
    /// Corruption fight.
    /// </summary>
    public static bool ShadowSightApplies(bool inShadowFight, bool holdsLantern) => inShadowFight || holdsLantern;

    /// <summary>Cards shaded per turn: one in a Shadow Corruption fight, plus one for holding the Lantern (in any fight).</summary>
    public static int ShadesPerTurn(bool inShadowFight, bool holdsLantern) => (inShadowFight ? 1 : 0) + (holdsLantern ? 1 : 0);

    /// <summary>Statuses and curses are never shaded; neither is a card that is already shaded.</summary>
    public static bool CanShade(bool isStatusOrCurse, bool alreadyShaded) => !isStatusOrCurse && !alreadyShaded;

    /// <summary>
    /// Index of the hand card to shade this turn among <paramref name="candidateCount"/> candidates, or -1. <paramref name="pick"/>
    /// numbers the shades of the same turn (0 for the first; later picks use their own stream).
    /// </summary>
    public static int PickShadeIndex(ulong runSeed, string locationKey, ulong playerId, int turnNumber, int candidateCount, int pick = 0) =>
        candidateCount <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "shadow.shade:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + turnNumber.ToString(CultureInfo.InvariantCulture)
                    + (pick > 0 ? ":" + pick.ToString(CultureInfo.InvariantCulture) : string.Empty))
                .NextInt(candidateCount);

    /// <summary>The shop card slots to shade: 4 distinct slot indices (fewer if the shop has fewer cards), in ascending order.</summary>
    public static IReadOnlyList<int> PickShopShadedSlots(ulong runSeed, string locationKey, ulong playerId, int cardSlots)
    {
        var rng = ZoneRandom.ForStream(runSeed, "shadow.shop:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        List<int> slots = Enumerable.Range(0, Math.Max(0, cardSlots)).ToList();
        var picked = new List<int>();
        while (picked.Count < ShopShadedCards && slots.Count > 0)
        {
            int index = rng.NextInt(slots.Count);
            picked.Add(slots[index]);
            slots.RemoveAt(index);
        }

        picked.Sort();
        return picked;
    }

    /// <summary>A shaded or hidden shop item's price: 60% off, rounded to nearest (halves up).</summary>
    public static decimal DiscountedPrice(decimal cost) =>
        cost <= 0 ? cost : Math.Round(cost * (100 - ShopDiscountPercent) / 100m, MidpointRounding.AwayFromZero);

    /// <summary>Whether this Troubled Dreams adds a curse: a 50% roll from the run seed, location and player.</summary>
    public static bool TroubledDreamsCurses(ulong runSeed, string locationKey, ulong playerId) =>
        ZoneRandom.ForStream(runSeed, "shadow.dreams.roll:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture))
            .NextInt(100) < TroubledDreamsCursePercent;

    /// <summary>Index of the curse Troubled Dreams adds, among <paramref name="curseCount"/> candidates, or -1.</summary>
    public static int PickCurseIndex(ulong runSeed, string locationKey, ulong playerId, int curseCount) =>
        curseCount <= 0
            ? -1
            : ZoneRandom.ForStream(runSeed, "shadow.dreams:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture))
                .NextInt(curseCount);

    public static int DawnGold(int nodes) => Math.Max(0, nodes) * DawnGoldPerNode;

    /// <summary>Dawn healing: 10% of Max HP per node, rounded down (the heal itself never goes above Max HP).</summary>
    public static int DawnHeal(int maxHp, int nodes) => Math.Max(0, maxHp) * DawnHealPercentPerNode * Math.Max(0, nodes) / 100;

    public static bool DawnGivesRelic(int nodes) => nodes >= DawnRelicMinNodes;

    /// <summary>Key of a map node in the saved node sets (revealed nodes, the current Dawn stay): "act:row:col".</summary>
    public static string NodeKey(int actIndex, int row, int col) =>
        actIndex.ToString(CultureInfo.InvariantCulture) + ":" + row.ToString(CultureInfo.InvariantCulture) + ":" + col.ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses a saved node set ("key;key;..."), ignoring empty entries.</summary>
    public static HashSet<string> ParseNodeSet(string? saved) =>
        new((saved ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    /// <summary>Adds <paramref name="keys"/> to a saved node set; returns the new value, entries in ordinal order.</summary>
    public static string AddToNodeSet(string? saved, IEnumerable<string> keys)
    {
        HashSet<string> set = ParseNodeSet(saved);
        set.UnionWith(keys.Where(key => !string.IsNullOrEmpty(key)));
        return string.Join(";", set.OrderBy(key => key, StringComparer.Ordinal));
    }
}
