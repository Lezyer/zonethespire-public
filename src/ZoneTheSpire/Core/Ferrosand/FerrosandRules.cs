using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Ferrosand;

/// <summary>
/// Engine-free Ferrosand rules: Magnetized damage dampening, Ferroform block, Magnetic cards, Magnetize and the magnetic
/// shop. Random picks use the run seed plus synced combat/shop state (never a game RNG stream), so every multiplayer peer
/// gets the same results.
/// </summary>
public static class FerrosandRules
{
    public const int DampenedDamage = 1;
    public const int MagnetizeCardCount = 3;
    public const int ShopMagneticCount = 3;
    public const int MaxPullsPerTurn = 3;
    public const int LargeFieldstoneMaxPullsPerTurn = 6;
    public const int LargeFieldstoneMagnetizeCardCount = 3;
    public const int FerroformBlockPercent = 25;

    public static string MagneticCardText => "[gold]Magnetic[/gold].";

    /// <summary>Magnetized charges: one dampened hit per turn for each player in the fight.</summary>
    public static int MagnetizedAmount(int playerCount) => Math.Max(1, playerCount);

    /// <summary>Whether this unblocked damage is dampened: a hit that does damage while charges remain this turn.</summary>
    public static bool ShouldDampen(int unblockedDamage, int chargesUsed, int charges) =>
        unblockedDamage > 0 && chargesUsed < charges;

    /// <summary>HP a dampened hit takes: 1 (a hit that would already deal less stays as it is).</summary>
    public static int DampenedHpLoss(int unblockedDamage) => Math.Min(unblockedDamage, DampenedDamage);

    /// <summary>
    /// Block Ferroform gains when its owner's attack breaks through a player's block: a quarter of the block it removed, rounded
    /// down, at least 1 when it removed any.
    /// </summary>
    public static int FerroformBlock(int blockedDamage) =>
        blockedDamage <= 0 ? 0 : Math.Max(1, blockedDamage * FerroformBlockPercent / 100);

    /// <summary>A card can become Magnetic if it can be played and isn't Magnetic yet.</summary>
    public static bool CanMagnetize(bool unplayable, bool alreadyMagnetic) => !unplayable && !alreadyMagnetic;

    /// <summary>
    /// Whether a Magnetic card may still pull this turn: each player gets <see cref="MaxPullsPerTurn"/> pulls per turn, counting
    /// only cards actually moved into the hand.
    /// </summary>
    public static bool CanPull(int pullsThisTurn, bool hasLargeFieldstone = false) =>
        pullsThisTurn < PullLimit(hasLargeFieldstone);

    /// <summary>The Magnetic pull limit for a player, doubled while they hold Large Fieldstone.</summary>
    public static int PullLimit(bool hasLargeFieldstone) =>
        hasLargeFieldstone ? LargeFieldstoneMaxPullsPerTurn : MaxPullsPerTurn;

    /// <summary>How many random cards Magnetize magnetizes: 3, or every eligible card when fewer remain.</summary>
    public static int MagnetizeCount(int eligibleCards) => Math.Clamp(eligibleCards, 0, MagnetizeCardCount);

    /// <summary>How many cards Large Fieldstone can magnetize when obtained: up to 3 eligible cards.</summary>
    public static int LargeFieldstoneSelectCount(int eligibleCards) =>
        Math.Clamp(eligibleCards, 0, LargeFieldstoneMagnetizeCardCount);

    /// <summary>
    /// The deck indices (ascending) of up to <paramref name="count"/> random magnetizable cards: decided by the run seed, the map
    /// location and the player, so every peer magnetizes the same cards.
    /// </summary>
    public static IReadOnlyList<int> PickMagnetizeIndices(ulong runSeed, string locationKey, ulong playerId, int eligibleCards, int count)
    {
        var indices = Enumerable.Range(0, Math.Max(0, eligibleCards)).ToList();
        var rng = ZoneRandom.ForStream(runSeed, "ferrosand.magnetize:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        int picked = Math.Min(count, indices.Count);
        for (int i = 0; i < picked; i++)
        {
            int j = i + rng.NextInt(indices.Count - i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices.Take(picked).OrderBy(index => index).ToList();
    }

    /// <summary>
    /// Which Magnetic card a Magnetic card pulls from the draw pile, or -1 when there is none. Decided by the run seed, the
    /// player, their turn number and how many pulls they already made this combat, all identical on every peer.
    /// </summary>
    public static int PickPullIndex(ulong runSeed, ulong playerId, int turnNumber, int pullNumber, int candidates) =>
        candidates <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "ferrosand.pull:" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + turnNumber.ToString(CultureInfo.InvariantCulture) + ":" + pullNumber.ToString(CultureInfo.InvariantCulture))
                .NextInt(candidates);

    /// <summary>
    /// The slots (indices into the shop's stocked cards, ascending) that become Magnetic: 3 distinct slots, or all of them when
    /// fewer are stocked. Decided by the run seed, shop location and player.
    /// </summary>
    public static IReadOnlyList<int> PickShopMagneticSlots(ulong runSeed, string locationKey, ulong playerId, int stockedCards)
    {
        var slots = Enumerable.Range(0, Math.Max(0, stockedCards)).ToList();
        var rng = ZoneRandom.ForStream(runSeed, "ferrosand.shop:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        int count = Math.Min(ShopMagneticCount, slots.Count);
        for (int i = 0; i < count; i++)
        {
            int j = i + rng.NextInt(slots.Count - i);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        return slots.Take(count).OrderBy(slot => slot).ToList();
    }

    public const int AttuneMaxCards = 3;
    public const int AttuneHpLoss = 10;
    public const int ChargeMaxCards = 2;
    public const int ChargeMaxHpLoss = 4;
    public const int SiftGold = 40;

    /// <summary>The Singing Lodestone: Attune needs more HP than it costs and a card that can be magnetized.</summary>
    public static bool CanAttune(int currentHp, int magnetizableCards) => currentHp > AttuneHpLoss && magnetizableCards > 0;

    /// <summary>The Singing Lodestone: how many cards Charge the Iron upgrades: up to 2, or every eligible card when fewer remain.</summary>
    public static int ChargeSelectCount(int upgradableMagneticCards) => Math.Clamp(upgradableMagneticCards, 0, ChargeMaxCards);
}
