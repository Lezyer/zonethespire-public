using System;
using System.Collections.Generic;
using System.Globalization;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Hallowed;

/// <summary>
/// Engine-free Blinding Hallows rules: the Hallowed kill check, the Hallowed-into-Doom conversion, Zealous's share of an
/// enemy attack, and the seeded picks for Blasphemous marks, shop Hallowing and Repent. Picks use the run seed and synced
/// state (never a game RNG stream), so every multiplayer peer picks the same cards.
/// </summary>
public static class HallowedRules
{
    public const int HallowedPerBlasphemy = 5;
    public const int BlasphemousPerTurn = 2;
    public const int ZealousPercent = 15;
    public const int RepentHpCost = 10;
    public const int RepentAttackCount = 3;
    public const int ShopHallowingCount = 3;
    public const int RedemptionHallowedRemoved = 5;

    /// <summary>
    /// Hallowed that turns into Doom on an enemy with both, at the end of the enemy turn: half, rounded up, at least 1 while
    /// any Hallowed remains, so the conversion always makes progress.
    /// </summary>
    public static int ConvertToDoom(int hallowed) => hallowed <= 0 ? 0 : Math.Max(1, (hallowed + 1) / 2);

    /// <summary>Whether Hallowed kills its owner at the end of its side's turn, like Doom.</summary>
    public static bool IsHallowedKill(int currentHp, int hallowed) => hallowed > 0 && currentHp <= hallowed;

    /// <summary>
    /// Hallowed a Zealous enemy's attack gives the player it hits: 15% of the attack's full damage (after Strength, Vulnerable
    /// and the like, before Block), rounded down, at least 1. Non-attack damage gives none.
    /// </summary>
    public static int ZealousHallowed(decimal damage, bool poweredAttack) =>
        poweredAttack && damage > 0 ? Math.Max(1, (int)Math.Floor(damage * ZealousPercent / 100m)) : 0;

    /// <summary>Hallowed a Redemption card removes from its player when played: 5, or all of it when they have less.</summary>
    public static int RedemptionRemoval(int hallowed) => Math.Clamp(hallowed, 0, RedemptionHallowedRemoved);

    /// <summary>Repent needs more HP than it costs, so it can never kill.</summary>
    public static bool CanRepent(int currentHp) => currentHp > RepentHpCost;

    public static string BlasphemousStream(string locationKey, int turnNumber) =>
        "blasphemous:" + locationKey + ":" + turnNumber.ToString(CultureInfo.InvariantCulture);

    public static string ShopStream(string locationKey) => "shop:" + locationKey;

    public static string RepentStream(string locationKey) => "repent:" + locationKey;

    /// <summary>Up to <paramref name="count"/> distinct random indices into <paramref name="candidates"/>, ascending.</summary>
    public static IReadOnlyList<int> PickIndices(ulong runSeed, string streamKey, ulong playerId, int candidates, int count)
    {
        var all = new List<bool>();
        for (int i = 0; i < Math.Max(0, candidates); i++)
        {
            all.Add(true);
        }

        return PickPreferring(runSeed, streamKey, playerId, all, count);
    }

    /// <summary>
    /// Up to <paramref name="count"/> distinct random indices, ascending: candidates flagged in <paramref name="preferred"/>
    /// first, and the others only once every preferred one is taken. One seeded stream per key and player.
    /// </summary>
    public static IReadOnlyList<int> PickPreferring(ulong runSeed, string streamKey, ulong playerId, IReadOnlyList<bool> preferred, int count)
    {
        var first = new List<int>();
        var rest = new List<int>();
        for (int i = 0; i < preferred.Count; i++)
        {
            (preferred[i] ? first : rest).Add(i);
        }

        ZoneRandom rng = ZoneRandom.ForStream(runSeed, "blinding_hallowed." + streamKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        List<int> picked = Take(rng, first, Math.Max(0, count));
        picked.AddRange(Take(rng, rest, Math.Max(0, count) - picked.Count));
        picked.Sort();
        return picked;
    }

    /// <summary>A partial Fisher-Yates shuffle: the first <paramref name="take"/> items of a random order of the pool.</summary>
    private static List<int> Take(ZoneRandom rng, List<int> pool, int take)
    {
        int picked = Math.Min(Math.Max(0, take), pool.Count);
        for (int i = 0; i < picked; i++)
        {
            int j = i + rng.NextInt(pool.Count - i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, picked);
    }
}
