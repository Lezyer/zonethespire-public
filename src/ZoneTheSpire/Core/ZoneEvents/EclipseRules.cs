using System;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>Engine-free numbers for The Eclipse event (Prismatic Storm and Shadow Corruption).</summary>
public static class EclipseRules
{
    public const int LightTransformCount = 2;
    public const int DarkCorruptCount = 2;
    public const int LookChancePercent = 50;
    public const int LookMaxHpLoss = 10;

    /// <summary>How many cards Step into the Light transforms: 2, or every transformable card when fewer remain.</summary>
    public static int LightCountFor(int transformableCards) => Math.Clamp(transformableCards, 0, LightTransformCount);

    /// <summary>How many cards Step into the Dark corrupts: 2, or every eligible card when fewer remain.</summary>
    public static int DarkCountFor(int eligibleCards) => Math.Clamp(eligibleCards, 0, DarkCorruptCount);

    /// <summary>Look Straight at It: a roll in [0, 100) under 50 gives the relic and the full heal.</summary>
    public static bool LookSucceeds(int rollPercent) => rollPercent < LookChancePercent;

    public static bool CanLook(int maxHp) => maxHp > LookMaxHpLoss;

    /// <summary>
    /// Split Your Shadow picks among the cards that can be Shadow Corrupted, preferring ones that can still be upgraded (the copy
    /// is upgraded); only when none can be upgraded does it pick among all of them, and then the copy isn't upgraded.
    /// </summary>
    public static bool SplitPrefersUpgradable(int upgradableCandidates) => upgradableCandidates > 0;
}
