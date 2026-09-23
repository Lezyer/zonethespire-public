using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Phantasmal;

/// <summary>
/// Engine-free Phantasmal Tombs rules: Phantasm damage reduction, ghostly copies and revives, Phantasm-Haunted cards, the
/// Haunt campfire action and the raided shop. Random picks use the run seed and the map location (never a game RNG stream),
/// so every multiplayer peer gets the same results.
/// </summary>
public static class PhantasmalRules
{
    public const int PhantasmDamageReductionPercent = 25;
    public const int PhantasmDamageDealtPercent = 75;

    /// <summary>How much less attack damage a Phantasm deals, for its text.</summary>
    public const int PhantasmDamageDealtReductionPercent = 100 - PhantasmDamageDealtPercent;
    public const int CopyIntangible = 99;
    public const int ReviveHpPercent = 20;
    public const int ReviveIntangible = 1;
    public const int HauntedCostReduction = 1;
    public const int HauntedExhaustChancePercent = 30;
    public const int HauntHpCost = 5;
    public const int HauntCardCount = 3;
    public const int ShopDiscountPercent = 50;
    public const int RaidChancePercent = 50;
    public const int GhostJarChancePercent = 50;

    /// <summary>HP a Phantasm loses from <paramref name="amount"/> unblocked damage: 25% less, rounded down, at least 1.</summary>
    public static int ReducedHpLoss(int amount) =>
        amount <= 0 ? amount : Math.Max(1, amount * (100 - PhantasmDamageReductionPercent) / 100);

    /// <summary>
    /// Multiplier on a Phantasm's attack damage: 25% less for powered attacks, full damage once it has risen again (ghostly copies
    /// have no Phantasm, so they deal full damage too).
    /// </summary>
    public static decimal DealtDamageMultiplier(bool poweredAttack, bool hasRisen) =>
        poweredAttack && !hasRisen ? PhantasmDamageDealtPercent / 100m : 1m;

    /// <summary>
    /// Max HP of the ghostly copy a Phantasm leaves: 3 in act 1, 5 in act 2, 7 in act 3 and later, times the number of players in
    /// the fight (at least 1), e.g. 9 HP in act 1 with 3 players.
    /// </summary>
    public static int CopyHp(int actIndex, int playerCount) => Math.Max(1, playerCount) * (actIndex switch
    {
        <= 0 => 3,
        1 => 5,
        _ => 7,
    });

    /// <summary>
    /// A dying Phantasm leaves a ghostly copy unless it is a minion, a copy itself, or an enemy whose body stays to revive
    /// (those get <see cref="ShouldReviveOnce"/> instead).
    /// </summary>
    public static bool ShouldSpawnCopy(bool isEnemyMonster, bool hasPhantasm, bool isMinion, bool isCopy, bool keepsBody) =>
        isEnemyMonster && hasPhantasm && !isMinion && !isCopy && !keepsBody;

    /// <summary>A reviving Phantasm (body stays, e.g. Decimillipede segments) rises instantly the first time it dies.</summary>
    public static bool ShouldReviveOnce(bool isEnemyMonster, bool hasPhantasm, bool isCopy, bool keepsBody, bool alreadyRevived) =>
        isEnemyMonster && hasPhantasm && !isCopy && keepsBody && !alreadyRevived;

    /// <summary>HP a reviving Phantasm rises with: 20% of its Max HP, rounded to nearest, at least 1.</summary>
    public static int ReviveHp(int maxHp) =>
        Math.Max(1, (int)Math.Round(maxHp * ReviveHpPercent / 100m, MidpointRounding.AwayFromZero));

    /// <summary>A card can become Phantasm-Haunted if it is playable, has a real cost (or costs X) and isn't haunted yet.</summary>
    public static bool CanHaunt(bool costsX, int baseCost, bool unplayable, bool alreadyHaunted) =>
        (costsX || baseCost >= 0) && !unplayable && !alreadyHaunted;

    /// <summary>How much higher X counts on a haunted X-cost card (which keeps its cost).</summary>
    public const int HauntedXBonus = 1;

    /// <summary>A haunted card's combat cost: 1 less, never below 0.</summary>
    public static int HauntedCost(int cost) => Math.Max(0, cost - HauntedCostReduction);

    /// <summary>
    /// The line shown at the top of a haunted card: the keyword and exactly what playing it does. Haunted cards are
    /// Ethereal (the game exhausts them from the hand at the end of the turn) and roll to exhaust when played.
    /// </summary>
    public static string HauntedCardText =>
        $"[gold]Phantasm-Haunted[/gold]: [gold]Ethereal[/gold]. [blue]{HauntedExhaustChancePercent}%[/blue] [gold]Exhaust[/gold].";

    /// <summary>The line on a haunted X-cost card: X counts one higher, and the card is haunted like any other.</summary>
    public static string HauntedXCardText =>
        $"[gold]Phantasm-Haunted[/gold]: X+[blue]{HauntedXBonus}[/blue]. [gold]Ethereal[/gold]. [blue]{HauntedExhaustChancePercent}%[/blue] [gold]Exhaust[/gold].";

    /// <summary>Haunt needs more HP than it costs (it can't kill) and at least one card that can be haunted.</summary>
    public static bool CanPerformHaunt(int currentHp, int eligibleCards) => currentHp > HauntHpCost && eligibleCards > 0;

    /// <summary>How many cards Haunt haunts: 3, or every eligible card when fewer remain.</summary>
    public static int HauntSelectCount(int eligibleCards) => Math.Clamp(eligibleCards, 0, HauntCardCount);

    /// <summary>
    /// The indices (ascending) of up to <paramref name="count"/> random hauntable cards: decided by the run seed, the map
    /// location and the player, so every peer haunts the same cards.
    /// </summary>
    public static IReadOnlyList<int> PickHauntIndices(ulong runSeed, string locationKey, ulong playerId, int eligibleCards, int count)
    {
        var indices = Enumerable.Range(0, Math.Max(0, eligibleCards)).ToList();
        var rng = ZoneRandom.ForStream(runSeed, "phantasmal_tombs.haunt:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        int picked = Math.Min(count, indices.Count);
        for (int i = 0; i < picked; i++)
        {
            int j = i + rng.NextInt(indices.Count - i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices.Take(picked).OrderBy(index => index).ToList();
    }

    /// <summary>
    /// Index of the card reward option to haunt among the eligible ones, or -1. <paramref name="optionsKey"/> identifies the
    /// reward (its cards), so several rewards at the same node pick independently.
    /// </summary>
    public static int PickRewardHauntIndex(ulong runSeed, string locationKey, ulong playerId, string optionsKey, int eligibleCount) =>
        eligibleCount <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "phantasmal_tombs.haunted_reward:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + optionsKey)
                .NextInt(eligibleCount);

    public const int ProcessionMaxHpPercent = 30;
    public const int ProcessionApparitions = 2;
    public const int BottleHpLoss = 15;
    public const int BottledGhosts = 2;

    /// <summary>
    /// The Weeping Crypt: Join the Procession costs 30% of Max HP, rounded down, at least 1, but never the last point of Max HP.
    /// </summary>
    public static int ProcessionMaxHpLoss(int maxHp) =>
        maxHp <= 1 ? 0 : Math.Min(maxHp - 1, Math.Max(1, maxHp * ProcessionMaxHpPercent / 100));

    /// <summary>The Weeping Crypt: Bottle the Spirits needs more HP than it costs.</summary>
    public static bool CanBottleSpirits(int currentHp) => currentHp > BottleHpLoss;

    /// <summary>Raided shop price: 50% off, rounded to nearest (halves up).</summary>
    public static decimal DiscountedPrice(decimal cost) =>
        cost <= 0 ? cost : Math.Round(cost * (100 - ShopDiscountPercent) / 100m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Whether the ghosts took one shop item (50%): decided by the run seed, shop location, player, item category and slot,
    /// so every peer empties the same slots.
    /// </summary>
    /// <summary>
    /// Whether a player's rewards for the Phantasmal Tombs fight at <paramref name="locationKey"/> include a Ghost in a Jar (50%,
    /// rolled separately for each player).
    /// </summary>
    public static bool DropsGhostInAJar(ulong runSeed, string locationKey, ulong playerId) =>
        ZoneRandom.ForStream(runSeed, "phantasmal_tombs.ghost_jar:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture))
            .NextInt(100) < GhostJarChancePercent;

    public static bool IsRaided(ulong runSeed, string locationKey, ulong playerId, string category, int slot) =>
        ZoneRandom.ForStream(
                runSeed,
                "phantasmal_tombs.raid:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + category + ":" + slot.ToString(CultureInfo.InvariantCulture))
            .NextInt(100) < RaidChancePercent;
}
