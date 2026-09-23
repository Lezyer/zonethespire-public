using System;

namespace ZoneTheSpire.Core.BloodRain;

/// <summary>Engine-free Blood Rain rules: Blood Drinker healing and the Max HP reward for winning a fight.</summary>
public static class BloodRainRules
{
    public const int HealPercent = 70;
    public const int NormalMaxHpReward = 3;
    public const int EliteMaxHpReward = 5;

    /// <summary>
    /// Blood Drinker heal for <paramref name="damageDealt"/> HP of damage: <paramref name="percent"/>% rounded to nearest
    /// (halves up), at least 1 whenever any damage got through.
    /// </summary>
    public static int HealAmount(int damageDealt, int percent)
    {
        if (damageDealt <= 0 || percent <= 0)
        {
            return 0;
        }

        int heal = (int)Math.Round(damageDealt * percent / 100m, MidpointRounding.AwayFromZero);
        return Math.Max(1, heal);
    }

    public const int GoldPerHp = 15;

    /// <summary>Blood Rain shop price in HP: the gold price / 15, rounded to nearest (halves up), at least 1 for a paid item.</summary>
    public static int HpCost(int goldCost) =>
        goldCost <= 0 ? 0 : Math.Max(1, (int)Math.Round(goldCost / (decimal)GoldPerHp, MidpointRounding.AwayFromZero));

    /// <summary>A Blood Rain purchase must leave the player alive: it needs more current HP than it costs.</summary>
    public static bool CanAffordHp(int currentHp, int hpCost) => hpCost < currentHp;

    public const int SacrificeMaxHpCost = 10;

    /// <summary>Blood Sacrifice is only offered when losing its Max HP cost leaves at least 1 Max HP.</summary>
    public static bool CanSacrifice(int maxHp) => maxHp > SacrificeMaxHpCost;

    /// <summary>Max HP every player gains for winning a Blood Rain fight.</summary>
    public static int MaxHpReward(bool isElite) => isElite ? EliteMaxHpReward : NormalMaxHpReward;

    public const int UmbrellaGoldCost = 150;
    public const int BottleMaxHpLoss = 10;
    public const int JarHealPercent = 75;
    public const int ShrugHeal = 15;
    public const int UmbrellaBuffer = 1;

    /// <summary>The Umbrella Seller: Buy an Umbrella needs the full 150 gold.</summary>
    public static bool CanBuyUmbrella(int gold) => gold >= UmbrellaGoldCost;

    /// <summary>The Umbrella Seller: Bottle Your Blood needs more Max HP than it costs.</summary>
    public static bool CanBottle(int maxHp) => maxHp > BottleMaxHpLoss;

    /// <summary>Jar of Blood: HP restored when it prevents death, 75% of Max HP rounded down, at least 1.</summary>
    public static int JarHeal(int maxHp) => System.Math.Max(1, maxHp * JarHealPercent / 100);
}
