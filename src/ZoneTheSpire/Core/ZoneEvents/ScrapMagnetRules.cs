using System;

namespace ZoneTheSpire.Core.ZoneEvents;

/// <summary>Engine-free numbers for The Scrap Magnet event (Scrapyard and Ferrosand).</summary>
public static class ScrapMagnetRules
{
    public const int LeverMagnetizeCount = 2;
    public const int ThrowScrapGold = 40;
    public const int ShakeRelicChancePercent = 50;
    public const int ShakeHpLoss = 5;

    /// <summary>How many cards Pull the Lever makes Magnetic: 2, or every eligible card when fewer remain.</summary>
    public static int LeverMagnetizeCountFor(int eligibleCards) => Math.Clamp(eligibleCards, 0, LeverMagnetizeCount);

    /// <summary>Shake It Loose: a roll in [0, 100) under 50 finds a relic (with the potion); otherwise scrap hits you.</summary>
    public static bool ShakeFindsRelic(int rollPercent) => rollPercent < ShakeRelicChancePercent;

    /// <summary>Shake It Loose may lose 5 HP, so it is locked while that would kill.</summary>
    public static bool CanShake(int currentHp) => currentHp > ShakeHpLoss;
}
