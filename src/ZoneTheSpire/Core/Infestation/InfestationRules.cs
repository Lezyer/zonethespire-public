namespace ZoneTheSpire.Core.Infestation;

/// <summary>Engine-free Infestation rules: which deaths release a Wriggler, and the first move of a slotless Wriggler.</summary>
public static class InfestationRules
{
    public const string BiteMoveId = "NASTY_BITE_MOVE";
    public const string WriggleMoveId = "WRIGGLE_MOVE";

    /// <summary>
    /// A Wriggler crawls out when an enemy monster dies, except minions, the Phrog Parasite (its Infested power already
    /// releases its own Wrigglers), Wrigglers the Infestation spawned (no chains) and enemies that already released one
    /// (reviving enemies such as Decimillipede segments release only on their first death).
    /// </summary>
    public static bool ShouldSpawnWriggler(bool isEnemyMonster, bool isMinion, bool isPhrogParasite, bool spawnedByInfestation, bool alreadyReleased) =>
        isEnemyMonster && !isMinion && !isPhrogParasite && !spawnedByInfestation && !alreadyReleased;

    /// <summary>
    /// Vanilla Wrigglers pick their first move from their encounter slot (wriggler1/3 bite, wriggler2/4 wriggle). Slotless
    /// Wrigglers alternate the same way by spawn order.
    /// </summary>
    public static string InitialMoveId(int spawnIndex) => (spawnIndex & 1) == 0 ? BiteMoveId : WriggleMoveId;

    /// <summary>Extra Max HP for Wrigglers the Infestation spawns, by act: +15% in act 1, +30% in act 2, +60% in act 3 and later.</summary>
    public static int WrigglerHpBonusPercent(int actIndex) => actIndex switch
    {
        <= 0 => 15,
        1 => 30,
        _ => 60,
    };

    /// <summary>A Wriggler's boosted Max HP: <paramref name="maxHp"/> plus the act's bonus, rounded to nearest (halves up).</summary>
    public static int BoostedWrigglerMaxHp(int maxHp, int actIndex) =>
        (int)System.Math.Round(maxHp * (100 + WrigglerHpBonusPercent(actIndex)) / 100m, System.MidpointRounding.AwayFromZero);

    public const int ReachInHpLoss = 12;
    public const int NestMaxCards = 3;

    /// <summary>The Writhing Pit: Reach In needs more HP than it costs.</summary>
    public static bool CanReachIn(int currentHp) => currentHp > ReachInHpLoss;

    /// <summary>The Writhing Pit: Let Them Nest works on playable cards (a card that already has Wriggling gains more).</summary>
    public static bool CanNest(bool unplayable) => !unplayable;
}
