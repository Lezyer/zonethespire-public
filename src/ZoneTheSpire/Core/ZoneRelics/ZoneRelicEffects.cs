using System;
using System.Collections.Generic;
using ZoneTheSpire.Core.Hallowed;

namespace ZoneTheSpire.Core.ZoneRelics;

public readonly record struct GoldenWishmakerResult(int HpLoss, int GoldCost)
{
    public bool Triggered => GoldCost > 0;
}

/// <summary>Engine-free numbers for zone relic effects.</summary>
public static class ZoneRelicEffects
{
    public const int CrimsonToothHealPercent = 5;
    public const int MarblePauldronsPolishing = 5;
    public const int GoldenWishmakerGoldPerDamage = 5;
    public const int FlutteringPhantasmHeal = 3;
    public const int PrismCloudCardCount = 2;
    public const int ScrollOfChantsCards = 3;
    public const int ScrollOfChantsKarma = 2;
    public const int FrostheartEnemyCold = 5;
    public const int SacrosanctFlailAttacks = 3;
    public const int SacrosanctFlailMultiplier = 2;
    public const int FrostheartSelfCold = 3;

    /// <summary>
    /// Crimson Tooth heal for <paramref name="hpLost"/> HP an enemy just lost, after <paramref name="hpLostBefore"/> earlier this
    /// combat: 5% of the running total, rounded down, minus what earlier hits already healed. Fractions carry over, so the
    /// total healed is always 5% of the total dealt, rounded down.
    /// </summary>
    public static int CrimsonToothHeal(int hpLostBefore, int hpLost)
    {
        int before = Math.Max(0, hpLostBefore);
        int total = before + Math.Max(0, hpLost);
        return total * CrimsonToothHealPercent / 100 - before * CrimsonToothHealPercent / 100;
    }

    /// <summary>Whether this hit is the first enemy hit that can break the owner's nonzero Block this combat.</summary>
    public static bool ReflectiveShardTriggers(bool usedThisCombat, int block, int incomingDamage, bool unblockable, bool dealerIsEnemy) =>
        !usedThisCombat && dealerIsEnemy && !unblockable && block > 0 && incomingDamage >= block;

    /// <summary>
    /// Resolves a potentially lethal HP loss for Golden Wishmaker. It only activates when the player can pay the full cost
    /// of staying at 1 HP; otherwise the original loss passes through and no gold is spent.
    /// </summary>
    public static GoldenWishmakerResult GoldenWishmaker(int currentHp, int incomingHpLoss, int gold)
    {
        int hp = Math.Max(0, currentHp);
        int loss = Math.Max(0, incomingHpLoss);
        if (hp <= 0 || loss < hp)
        {
            return new GoldenWishmakerResult(loss, 0);
        }

        int survivingLoss = hp - 1;
        int prevented = loss - survivingLoss;
        long cost = (long)prevented * GoldenWishmakerGoldPerDamage;
        if (gold < cost)
        {
            return new GoldenWishmakerResult(loss, 0);
        }

        return new GoldenWishmakerResult(survivingLoss, (int)cost);
    }

    /// <summary>Sacrosanct Flail: the Hallowed applied to a random enemy when its owner gains <paramref name="gained"/>.</summary>
    public static int SacrosanctFlailHallowed(int gained) => gained <= 0 ? 0 : gained * SacrosanctFlailMultiplier;

    /// <summary>
    /// Sacrosanct Flail's pickup: which of the owner's attacks (in deck order) gain Hallowing and Blasphemous, preferring
    /// those flagged in <paramref name="fresh"/> (without either yet). Seeded per player.
    /// </summary>
    public static IReadOnlyList<int> PickSacrosanctFlailAttacks(ulong runSeed, ulong playerId, IReadOnlyList<bool> fresh) =>
        HallowedRules.PickPreferring(runSeed, "sacrosanct_flail", playerId, fresh, SacrosanctFlailAttacks);

    /// <summary>The most options a card reward screen supports besides its cards (the game throws beyond this).</summary>
    public const int MaxCardRewardAlternatives = 2;

    /// <summary>
    /// Brew Extractor offers its Extract option whenever the screen has room for another option (Skip, reroll and Pael's Wing
    /// also use them). A full potion belt doesn't matter: the potion then comes as a potion reward.
    /// </summary>
    public static bool BrewExtractorOffers(int existingAlternatives) => existingAlternatives < MaxCardRewardAlternatives;
}
