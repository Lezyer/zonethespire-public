using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Hoarfrost;

/// <summary>
/// Engine-free Hoarfrost rules: how much Biting Cold a card carries, what Biting Cold does to an attack, and which cards freeze
/// each turn. Picks use the run seed and synced combat state (never a game RNG stream), so every multiplayer peer freezes the
/// same cards and ices the same enemies.
/// </summary>
public static class FrostRules
{
    public const int FrozenPerTurn = 2;
    public const int RewardColdPerCost = 3;
    public const int ShopColdPerCost = 3;
    public const int FrostbindColdPerCost = 4;
    public const int ColdSpentPerHit = 1;

    /// <summary>
    /// Biting Cold a card carries at <paramref name="perCost"/> per energy of its cost. A 0-cost card counts as 1, so it gets the
    /// plain rate; X-cost cards also count as 1 here and multiply by the energy spent when played.
    /// </summary>
    public static int ColdFor(int perCost, int energyCost, bool costsX) => perCost * Math.Max(1, costsX ? 1 : energyCost);

    /// <summary>Biting Cold on a card in a Hoarfrost card reward: 3 per energy of its cost.</summary>
    public static int RewardCold(int energyCost, bool costsX) => ColdFor(RewardColdPerCost, energyCost, costsX);

    /// <summary>Biting Cold on a card sold in a Hoarfrost shop: 3 per energy of its cost.</summary>
    public static int ShopCold(int energyCost, bool costsX) => ColdFor(ShopColdPerCost, energyCost, costsX);

    /// <summary>Biting Cold Frostbind adds to a card: 4 per energy of its cost, on top of any it already has.</summary>
    public static int FrostbindCold(int energyCost, bool costsX) => ColdFor(FrostbindColdPerCost, energyCost, costsX);

    /// <summary>Biting Cold applied when the card is played: X-cost cards multiply by the energy spent (at least 1).</summary>
    public static int PlayAmount(int amount, bool costsX, int energySpent) =>
        costsX ? amount * Math.Max(1, energySpent) : amount;

    /// <summary>Extra damage an attack deals to an iced enemy: its whole Biting Cold, added like Strength.</summary>
    public static int DamageBonus(int bitingCold) => Math.Max(0, bitingCold);

    /// <summary>Biting Cold left after a hit lands: one less, never below 0.</summary>
    public static int ColdAfterHit(int bitingCold) => Math.Max(0, bitingCold - ColdSpentPerHit);

    /// <summary>The line at the top of a card's text.</summary>
    public static string CardText(int amount, bool costsX) =>
        costsX
            ? $"[gold]{FrostText.BitingCold}[/gold] [blue]{amount.ToString(CultureInfo.InvariantCulture)}[/blue]X"
            : $"[gold]{FrostText.BitingCold}[/gold] [blue]{amount.ToString(CultureInfo.InvariantCulture)}[/blue]";

    /// <summary>How many cards freeze this turn: 2, or the whole hand when it holds fewer.</summary>
    public static int FreezeCountFor(int cardsInHand) => Math.Clamp(cardsInHand, 0, FrozenPerTurn);

    /// <summary>
    /// Which cards freeze: <paramref name="count"/> distinct indices into the hand (ascending). Decided by the run seed, player
    /// and turn, so every peer freezes the same cards.
    /// </summary>
    public static IReadOnlyList<int> PickFrozenIndices(ulong runSeed, string locationKey, ulong playerId, int turnNumber, int cardsInHand, int count)
    {
        var pool = Enumerable.Range(0, Math.Max(0, cardsInHand)).ToList();
        var rng = ZoneRandom.ForStream(
            runSeed,
            "hoarfrost.freeze:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture)
            + ":" + turnNumber.ToString(CultureInfo.InvariantCulture));
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

    /// <summary>
    /// Which enemy a card that didn't attack one ices, as an index into the living enemies, or -1 when there are none.
    /// <paramref name="playKey"/> tells separate plays apart, so two such cards in a turn can ice different enemies.
    /// </summary>
    public static int PickIcedEnemyIndex(ulong runSeed, string locationKey, ulong playerId, string playKey, int livingEnemies) =>
        livingEnemies <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "hoarfrost.ice:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + playKey)
                .NextInt(livingEnemies);
}
