using System.Collections.Generic;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Hoarfrost;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>
/// The Still Monk (a zone event shared by Hoarfrost and Deva's Domain): its numbers, locks and seeded picks.
/// Sit With Him: lose 15 HP; a chosen Attack or Skill gains Chakra 2 and Biting Cold 3 per energy it costs.
/// Share Your Fire: pay 90 Gold; 2 random upgradable Attacks or Skills are upgraded and gain Chakra 1.
/// Empty Your Pack: lose 5 Max HP; remove 2 chosen cards. Walk On (nothing) only appears when the other three are all locked.
/// See docs/superpowers/plans/2026-09-22-the-still-monk.md for the balance reasoning.
/// </summary>
public static class StillMonkRules
{
    public const int SitHpLoss = 15;
    public const int SitChakra = 2;
    public const int SitColdPerCost = FrostRules.RewardColdPerCost;
    public const int ShareGold = 90;
    public const int ShareCards = 2;
    public const int ShareChakra = 1;
    public const int PackMaxHpLoss = 5;
    public const int PackCards = 2;

    public static bool CanSit(int currentHp, int eligibleCards) => currentHp > SitHpLoss && eligibleCards > 0;

    public static bool CanShare(int gold, int upgradableCards) => gold >= ShareGold && upgradableCards > 0;

    public static bool CanEmptyPack(int removableCards) => removableCards >= PackCards;

    public static bool ShowsWalkOn(bool canSit, bool canShare, bool canEmptyPack) => !canSit && !canShare && !canEmptyPack;

    /// <summary>Sit With Him's Biting Cold for a card of this cost (a 0-cost card counts as 1, as everywhere else).</summary>
    public static int SitCold(int energyCost, bool costsX) => FrostRules.ColdFor(SitColdPerCost, energyCost, costsX);

    /// <summary>Share Your Fire's cards (indices into the eligible cards): distinct, seeded per node and player.</summary>
    public static IReadOnlyList<int> PickShared(ulong runSeed, string locationKey, ulong playerId, int eligibleCards) =>
        HallowedRules.PickIndices(runSeed, "still_monk.share:" + locationKey, playerId, eligibleCards, ShareCards);
}
