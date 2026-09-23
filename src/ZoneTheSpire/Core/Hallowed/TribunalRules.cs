using System;
using System.Collections.Generic;

namespace ZoneTheSpire.Core.Hallowed;

/// <summary>
/// The Sacred Tribunal (Blinding Hallows zone event): its numbers, locks and seeded picks.
/// Confess: lose 10 HP; every card loses Blasphemous and Redemption (Hallowing stays); 2 Max HP per Redemption lost.
/// Deny: every card that isn't upgraded is upgraded; each card it upgraded also gains Blasphemous, unless it had Redemption.
/// Devote: add Inquisitor's Wrath to the deck, then 2 random upgraded cards are downgraded.
/// </summary>
public static class TribunalRules
{
    public const int ConfessHpCost = 10;
    public const int MaxHpPerRedemption = 2;
    public const int DevoteDowngrades = 2;
    public const int InquisitorsWrathCost = 2;

    public static bool CanConfess(int currentHp) => currentHp > ConfessHpCost;

    public static int ConfessMaxHp(int redemptionsLost) => Math.Max(0, redemptionsLost) * MaxHpPerRedemption;

    public static bool CanDeny(int upgradableCards) => upgradableCards > 0;

    /// <summary>Deny makes a card Blasphemous only if Deny itself upgraded it, it has no Redemption, and it can carry the modifier.</summary>
    public static bool DenyBlasphemes(bool upgradedByDeny, bool hasRedemption, bool canCarry) => upgradedByDeny && !hasRedemption && canCarry;

    public static bool CanDevote(int upgradedCards) => upgradedCards >= DevoteDowngrades;

    /// <summary>The upgraded cards (indices into the player's upgraded cards) Devote downgrades: distinct, seeded per node and player.</summary>
    public static IReadOnlyList<int> PickDowngrades(ulong runSeed, string locationKey, ulong playerId, int upgradedCards) =>
        HallowedRules.PickIndices(runSeed, "tribunal.devote:" + locationKey, playerId, upgradedCards, DevoteDowngrades);
}
