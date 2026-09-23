using System;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>Engine-free numbers for The Tithe Collector event (Halls of Midas and Blood Rain) and its Blood Ledger relic.</summary>
public static class TitheRules
{
    public const int GoldTitheMinGold = 20;
    public const int GoldPerMaxHp = 10;
    public const int BloodTitheHpLoss = 12;
    public const int BloodTitheGildCount = 2;
    public const int LedgerMaxHpLoss = 5;
    public const int RobChancePercent = 50;
    public const int RobGold = 150;
    public const int RobHpLoss = 10;

    /// <summary>Tithe in Gold pays half the player's gold, rounded down.</summary>
    public static int GoldTithe(int gold) => Math.Max(0, gold) / 2;

    /// <summary>Max HP bought by a gold tithe: 1 per 10 gold paid, rounded down.</summary>
    public static int MaxHpForGold(int goldPaid) => Math.Max(0, goldPaid) / GoldPerMaxHp;

    public static bool CanTitheGold(int gold) => gold >= GoldTitheMinGold;

    public static bool CanTitheBlood(int currentHp) => currentHp > BloodTitheHpLoss;

    /// <summary>How many cards Tithe in Blood gilds: 2, or every eligible card when fewer remain.</summary>
    public static int GildCountFor(int eligibleCards) => Math.Clamp(eligibleCards, 0, BloodTitheGildCount);

    public static bool CanSignLedger(int maxHp) => maxHp > LedgerMaxHpLoss;

    public static bool CanRob(int currentHp) => currentHp > RobHpLoss;

    /// <summary>Rob the Collector: a roll in [0, 100) under 50 gets away with the gold.</summary>
    public static bool RobSucceeds(int rollPercent) => rollPercent < RobChancePercent;

    /// <summary>
    /// Blood Ledger: a shop price the holder can't afford in gold is paid in HP instead (1 HP per 15 gold, like Blood Rain
    /// shops), if they would survive it. Prices they can afford are paid in gold as usual.
    /// </summary>
    public static bool LedgerPaysWithHp(int gold, int price, int currentHp) =>
        price > 0 && gold < price && BloodRainRules.CanAffordHp(currentHp, BloodRainRules.HpCost(price));
}
