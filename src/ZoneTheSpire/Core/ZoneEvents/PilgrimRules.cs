using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>
/// Engine-free rules of the Troubled Pilgrim (Deva's Domain): giving Chakra away, selling it for gold, and the Worldly
/// Attachment relic's extra turns. Picks use the run seed and synced state, so every multiplayer peer gets the same results.
/// </summary>
public static class PilgrimRules
{
    public const int ChakraGiftCards = 2;
    public const int ChakraGiftAmount = 2;
    public const int GoldPerChakra = 50;
    public const int AttachmentTurns = 2;
    public const int AttachmentHealPercent = 10;

    /// <summary>Gold for giving up Chakra: 50 per point. Only positive Chakra pays; negative Chakra is ignored.</summary>
    public static int GoldFor(IEnumerable<int> chakraAmounts) =>
        chakraAmounts.Where(amount => amount > 0).Sum() * GoldPerChakra;

    /// <summary>Worldly Attachment: HP healed when its holder survives the fight (10% of Max HP, rounded down, at least 1).</summary>
    public static int AttachmentHeal(int maxHp) =>
        maxHp <= 0 ? 0 : Math.Max(1, maxHp * AttachmentHealPercent / 100);

    /// <summary>Worldly Attachment: the turns left after one of the holder's turns ends.</summary>
    public static int TurnsLeft(int turns) => Math.Max(0, turns - 1);

    /// <summary>How many cards are given Chakra: 2, or every eligible card when fewer.</summary>
    public static int GiftCountFor(int eligibleCards) => Math.Clamp(eligibleCards, 0, ChakraGiftCards);

    /// <summary>
    /// Which pile the next card is drawn from: cards that cost something come first, and 0-cost cards are only used once those
    /// run out.
    /// </summary>
    public static bool DrawsFromCostingCards(int costingCardsLeft) => costingCardsLeft > 0;
}
