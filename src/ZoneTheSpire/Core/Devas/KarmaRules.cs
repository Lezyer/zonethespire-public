using System;
using System.Globalization;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Devas;

/// <summary>
/// Engine-free rules of Karma and Chakra (Deva's Domain). Karma is a per-card combat value: positive Karma lowers the card's cost
/// (only the discount actually used is spent when it is played), negative Karma raises it. X-cost cards instead get X+1 for 1
/// positive Karma, or lose their negative Karma from X (never below 0). Chakra is a permanent card value: the card starts every
/// combat with that much Karma. Samsara (Deva's Domain fights) takes 1 more Karma from every played card, and gives 1 back to
/// negative-Karma cards left unplayed in hand at the end of the turn.
/// </summary>
public static class KarmaRules
{
    public const int MeditateChakra = 2;
    public const int MeditateHealPercent = 15;
    public const int RewardChakraMin = 1;
    public const int RewardChakraMax = 3;
    public const int SamsaraKarmaLossPerPlay = 1;
    public const int XKarmaBonus = 1;
    public const int ShopRelics = 6;
    public const int ShopPotions = 8;

    /// <summary>A normal card's cost after its Karma: lowered by positive Karma (not below 0), raised by negative Karma.</summary>
    public static int CostWithKarma(int cost, int karma) =>
        karma >= 0 ? Math.Max(0, cost - karma) : cost - karma;

    /// <summary>
    /// Karma a play of a normal card spends: the discount it actually got (cost without Karma minus the cost paid), never more
    /// than its Karma. Negative Karma is never spent.
    /// </summary>
    public static int KarmaSpent(int karma, int costWithoutKarma, int costWithKarma) =>
        karma <= 0 ? 0 : Math.Clamp(costWithoutKarma - costWithKarma, 0, karma);

    /// <summary>
    /// How an X-cost card's Karma changes its X for one play: +1 for any positive Karma, its whole amount when negative.
    /// </summary>
    public static int XBonusFor(int karma) => karma > 0 ? XKarmaBonus : karma < 0 ? karma : 0;

    /// <summary>Karma an X-cost play spends: 1 when it had positive Karma, else none.</summary>
    public static int XKarmaSpent(int karma) => karma > 0 ? XKarmaBonus : 0;

    /// <summary>
    /// A card that costs 0 has no discount to spend, so playing it passes 1 of its Karma to a random card in hand instead. With
    /// an empty hand there is nowhere for it to go, and the card keeps it.
    /// </summary>
    public static bool GivesKarmaAway(int karma, int costWithoutKarma, bool costsX, int cardsInHand) =>
        karma > 0 && costWithoutKarma == 0 && !costsX && cardsInHand > 0;

    /// <summary>
    /// The card in hand a 0-cost card passes its Karma to. Decided by the run seed, player, turn and how many were passed this
    /// combat, so every peer picks the same card.
    /// </summary>
    public static int PickKarmaGiftIndex(ulong runSeed, ulong playerId, int turnNumber, int giftNumber, int cardsInHand) =>
        cardsInHand <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "devas.karma.gift:" + playerId.ToString(CultureInfo.InvariantCulture)
                    + ":" + turnNumber.ToString(CultureInfo.InvariantCulture)
                    + ":" + giftNumber.ToString(CultureInfo.InvariantCulture))
                .NextInt(cardsInHand);

    /// <summary>An X value after the play's Karma bonus: never below 0 (then the card acts as if played for 0).</summary>
    public static int XWithKarma(int x, int bonus) => Math.Max(0, x + bonus);

    /// <summary>End-of-turn recovery (Samsara): a negative-Karma card left unplayed in hand gets 1 back, never above 0.</summary>
    public static int Recover(int karma) => karma < 0 ? karma + 1 : karma;

    /// <summary>Meditate: a card without Chakra gets Chakra 2; a card with Chakra has it doubled.</summary>
    public static int MeditatedChakra(int chakra) => chakra > 0 ? chakra * 2 : MeditateChakra;

    /// <summary>Meditate heals 15% of Max HP (rounded down, at least 1 when Max HP is positive).</summary>
    public static int MeditateHeal(int maxHp) => maxHp <= 0 ? 0 : Math.Max(1, maxHp * MeditateHealPercent / 100);

    /// <summary>The Karma line at the top of a card's text.</summary>
    public static string KarmaText(int karma) =>
        $"[gold]Karma[/gold] [blue]{karma.ToString(CultureInfo.InvariantCulture)}[/blue]";

    /// <summary>The Chakra line at the top of a card's text.</summary>
    public static string ChakraText(int chakra) =>
        $"[gold]Chakra[/gold] [blue]{chakra.ToString(CultureInfo.InvariantCulture)}[/blue]";

    /// <summary>
    /// Card reward (Deva's Domain): the index of the eligible option that gets Chakra, and how much (1-3), or (-1, 0).
    /// <paramref name="optionsKey"/> identifies the reward, so several rewards at the same node pick independently.
    /// </summary>
    public static (int Index, int Chakra) PickRewardChakra(ulong runSeed, string locationKey, ulong playerId, string optionsKey, int eligibleCount)
    {
        if (eligibleCount <= 0)
        {
            return (-1, 0);
        }

        var rng = ZoneRandom.ForStream(
            runSeed,
            "devas.chakra.reward:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture) + ":" + optionsKey);
        int index = rng.NextInt(eligibleCount);
        int chakra = RewardChakraMin + rng.NextInt(RewardChakraMax - RewardChakraMin + 1);
        return (index, chakra);
    }
}

/// <summary>
/// Where a Deva's Domain shop puts its slots on the merchant rug (rug coordinates, the top-left corner of each slot). With no cards
/// for sale, the 6 relics fill one row across the middle of the rug, where the character cards were, and the 8 potions sit in
/// two rows of 4 below them, in the vanilla relic and potion rows, clear of the card removal slot on the right.
/// </summary>
public static class DevasShopLayout
{
    public const float SlotStep = 150f;
    public const float RelicRowY = 451f;
    public const float RelicRowX = 421f;
    public const float PotionRowX = 571f;
    public static readonly float[] PotionRowsY = { 595f, 739f };
    public const int PotionsPerRow = 4;

    public static (float X, float Y) RelicSlot(int index) => (RelicRowX + SlotStep * index, RelicRowY);

    public static (float X, float Y) PotionSlot(int index) =>
        (PotionRowX + SlotStep * (index % PotionsPerRow), PotionRowsY[Math.Min(index / PotionsPerRow, PotionRowsY.Length - 1)]);
}
