using System;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Mirror;

/// <summary>Pure Mirrorlands rules. Integer maths and the mod's own RNG so every multiplayer peer computes the same result.</summary>
public static class MirrorRules
{
    public const int AloneCloneHpPercent = 25;
    public const int GroupCloneHpPercent = 50;

    /// <summary>Bonus Max HP for Decimillipede segments, which can't be mirrored.</summary>
    public const int SegmentMaxHpBonusPercent = 15;

    /// <summary>Max HP increased by <paramref name="percent"/>%, rounded up (distinct inputs stay distinct).</summary>
    public static int BoostedMaxHp(int maxHp, int percent) =>
        (Math.Max(0, maxHp) * (100 + Math.Max(0, percent)) + 99) / 100;

    /// <summary>True the first time a living creature is at or below half of its max HP.</summary>
    public static bool ShouldSplit(bool isAlive, int currentHp, int maxHp, bool hasSplit) =>
        isAlive && !hasSplit && maxHp > 0 && currentHp * 2 <= maxHp;

    /// <summary>25% when the fight started with at most one mirrorable enemy, otherwise 50%.</summary>
    public static int CloneHpPercent(int startingMirrorableEnemies) =>
        startingMirrorableEnemies <= 1 ? AloneCloneHpPercent : GroupCloneHpPercent;

    /// <summary><paramref name="percent"/>% of the original's max HP, rounded up, at least 1.</summary>
    public static int CloneMaxHp(int originalMaxHp, int percent) =>
        Math.Max(1, (Math.Max(0, originalMaxHp) * Math.Max(0, percent) + 99) / 100);

    /// <summary>
    /// Index of the enemy that gets Mirrored, or -1 without candidates. Derived from the run seed and the room id only
    /// (both identical on every peer), so no game RNG stream is consumed.
    /// </summary>
    public static int PickMirroredIndex(ulong runSeed, int roomId, int candidateCount) =>
        candidateCount <= 0
            ? -1
            : ZoneRandom.ForStream(runSeed, "mirrorlands.mirrored:" + StableHash.Inv(roomId)).NextInt(candidateCount);

    /// <summary>
    /// Index of the single card reward option that gets Glam, or -1 without eligible options. Derived from the run seed,
    /// the map location key and the rewarded player's id (identical on every peer), so no game RNG stream is consumed.
    /// </summary>
    public static int PickGlamIndex(ulong runSeed, string locationKey, ulong playerId, int eligibleCount) =>
        eligibleCount <= 0
            ? -1
            : ZoneRandom.ForStream(runSeed, "mirrorlands.glam:" + locationKey + ":" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .NextInt(eligibleCount);

    public const int ShakeHandsMaxHpLoss = 6;
    public const int TurnAwayGlamCount = 1;

    /// <summary>The Other You: Smash It pays several relics, because Bad Luck is a harsh curse to carry for the rest of the run.</summary>
    public const int SmashItRelics = 3;

    /// <summary>The Other You: Shake Hands needs more Max HP than it costs.</summary>
    public static bool CanShakeHands(int maxHp) => maxHp > ShakeHandsMaxHpLoss;
}
