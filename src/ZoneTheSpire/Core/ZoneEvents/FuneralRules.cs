using System;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>Engine-free numbers for the Your Own Funeral event (Phantasmal Tombs and Mirrorlands).</summary>
public static class FuneralRules
{
    public const int WillGold = 50;
    public const int ClimbInMaxHp = 20;
    public const int ClimbInHp = 1;
    public const int EulogyRemovals = 1;
    public const int EulogyUpgrades = 2;

    /// <summary>How many cards Give a Eulogy upgrades: 2, or every upgradable card when fewer remain.</summary>
    public static int EulogyUpgradeCount(int upgradableCards) => Math.Clamp(upgradableCards, 0, EulogyUpgrades);
}
