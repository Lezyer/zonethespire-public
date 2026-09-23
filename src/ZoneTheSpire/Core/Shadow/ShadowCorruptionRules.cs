using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Shadow;

/// <summary>
/// Engine-free rules of Shadow Corrupted cards: every value on the card is doubled, and playing it gives its owner Doom for the
/// energy actually paid (5 per energy, 3 if the cost was 0). One card in each card reward inside Shadow Corruption, and 3
/// cards in its shops, are corrupted. Picks use the run seed and synced state, so every multiplayer peer gets the same results.
/// </summary>
public static class ShadowCorruptionRules
{
    public const int ValueMultiplier = 2;
    public const int DoomPerEnergy = 5;
    public const int DoomWhenFree = 3;
    public const int ShopCorruptedCards = 3;

    /// <summary>Doom gained when a corrupted card is played: 5 per energy paid (X-cost cards: the X), or 3 if the cost was 0.</summary>
    public static int DoomFor(int energySpent) => energySpent <= 0 ? DoomWhenFree : energySpent * DoomPerEnergy;

    /// <summary>The line added to a corrupted card's text. <paramref name="currentCost"/> is the cost it would be paid at now.</summary>
    public static string CardText(bool costsX, int currentCost)
    {
        string doom = costsX ? $"{DoomPerEnergy}X" : DoomFor(currentCost).ToString(CultureInfo.InvariantCulture);
        return ZoneTheSpire.Core.Localization.ModText.Format("shadow.corrupted_card_line", ("Doom", doom));
    }

    /// <summary>
    /// Index of the card reward option to corrupt among the eligible ones, or -1. <paramref name="optionsKey"/> identifies the
    /// reward (its cards), so several rewards at the same node pick independently.
    /// </summary>
    public static int PickRewardIndex(ulong runSeed, string locationKey, ulong playerId, string optionsKey, int eligibleCount) =>
        eligibleCount <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "shadow.corrupt.reward:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + optionsKey)
                .NextInt(eligibleCount);

    /// <summary>
    /// The shop card slots to corrupt: 3 distinct indices into the eligible slots (all of them when fewer), in ascending order.
    /// Picked independently from the shaded slots.
    /// </summary>
    public static IReadOnlyList<int> PickShopSlots(ulong runSeed, string locationKey, ulong playerId, int eligibleSlots)
    {
        var rng = ZoneRandom.ForStream(runSeed, "shadow.corrupt.shop:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        List<int> remaining = Enumerable.Range(0, Math.Max(0, eligibleSlots)).ToList();
        var picked = new List<int>();
        while (picked.Count < ShopCorruptedCards && remaining.Count > 0)
        {
            int index = rng.NextInt(remaining.Count);
            picked.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        picked.Sort();
        return picked;
    }
}
