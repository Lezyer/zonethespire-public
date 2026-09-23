using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Fermentory;

/// <summary>
/// The Fermenting Vat (Fermentory zone event): its numbers, locks and seeded picks. Drink Deep gives Max HP and a random Curse;
/// Feed the Vat pours away every potion and upgrades one random card per potion.
/// </summary>
public static class FermentingVatRules
{
    public const int DrinkMaxHp = 10;
    public const int UpgradesPerPotion = 1;

    /// <summary>Feed the Vat needs a potion to pour and a card to upgrade.</summary>
    public static bool CanFeed(int potions, int upgradableCards) => potions > 0 && upgradableCards > 0;

    /// <summary>How many cards Feed the Vat upgrades: one per potion, never more than can be upgraded.</summary>
    public static int FeedUpgrades(int potions, int upgradableCards) =>
        Math.Clamp(Math.Max(0, potions) * UpgradesPerPotion, 0, Math.Max(0, upgradableCards));

    /// <summary>
    /// Which upgradable cards Feed the Vat upgrades: <paramref name="count"/> distinct indices (ascending), seeded by the run,
    /// the location and the player, so every peer upgrades the same cards.
    /// </summary>
    public static IReadOnlyList<int> PickUpgrades(ulong runSeed, string locationKey, ulong playerId, int upgradableCards, int count)
    {
        var pool = Enumerable.Range(0, Math.Max(0, upgradableCards)).ToList();
        var rng = ZoneRandom.ForStream(runSeed, "fermentory.vat:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        var picked = new List<int>();
        while (picked.Count < count && pool.Count > 0)
        {
            int index = rng.NextInt(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        picked.Sort();
        return picked;
    }
}
