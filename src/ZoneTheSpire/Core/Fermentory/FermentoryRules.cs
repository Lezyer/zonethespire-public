using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Fermentory;

/// <summary>What a special potion slot does. Saved by name, so members may be reordered but never renamed.</summary>
public enum SlotEffect
{
    DuplicatingSolution,
    EntropicSolvent,
    BottomlessMechanism,
    DistributionSystem,
    HealingBalm,
    EmpoweringDraught,
    WardingFlask,
    QuickeningTonic,
    VolatileMixture,
    RefillingStill,
}

/// <summary>A potion an enemy brews and drinks after acting.</summary>
public enum EnemyPotion
{
    Strength,
    Block,
    Regen,
    LiquidBronze,
    Weak,
    Vulnerable,
    Poison,
    Fire,
    Blood,
}

/// <summary>The extra reward a Fermentory fight gives.</summary>
public enum FightReward
{
    PotionSlot,
    Potion,
    SpecialSlot,
}

/// <summary>
/// Engine-free Fermentory rules: special slot effects and rolls, the saved slot map, enemy brewing and its numbers, and the extra
/// fight reward. Picks use the run seed, never a game RNG stream, so every multiplayer peer gets the same result.
/// </summary>
public static class FermentoryRules
{
    public const int HealingBalmHeal = 5;
    public const int EmpoweringStrength = 1;
    public const int WardingBlock = 6;
    public const int QuickeningDraw = 1;
    public const int VolatileDamage = 5;
    public const int KeepChancePercent = 50;
    public const int EntropicChancePercent = 50;
    public const int ShopPotionCount = 6;
    public const int BottlePrice = 75;
    public const int DistilGold = 50;
    public const int VisibleSlots = 7;
    public const int BloodHealPercent = 15;

    private static readonly IReadOnlyList<SlotEffect> AllEffects = Enum.GetValues<SlotEffect>();
    private static readonly IReadOnlyList<SlotEffect> SoloEffects = AllEffects.Where(effect => effect != SlotEffect.DistributionSystem).ToList();
    private static readonly IReadOnlyList<EnemyPotion> AllEnemyPotions = Enum.GetValues<EnemyPotion>();

    /// <summary>Effects a new special slot can roll: all 10, without Distribution System when playing alone.</summary>
    public static IReadOnlyList<SlotEffect> EffectPool(bool multiplayer) => multiplayer ? AllEffects : SoloEffects;

    /// <summary>
    /// A new special slot's effect, each equally likely (repeats allowed). <paramref name="rollIndex"/> is how many special
    /// slots the player already has, so two slots gained at the same place still roll independently.
    /// </summary>
    public static SlotEffect RollEffect(ulong runSeed, string locationKey, ulong playerId, int rollIndex, bool multiplayer)
    {
        IReadOnlyList<SlotEffect> pool = EffectPool(multiplayer);
        var rng = ZoneRandom.ForStream(runSeed, "fermentory.slot:" + locationKey + ":" + Inv(playerId) + ":" + Inv(rollIndex));
        return pool[rng.NextInt(pool.Count)];
    }

    /// <summary>The slot that becomes special next: the leftmost one that isn't special yet, or null when all are.</summary>
    public static int? NextSpecialSlot(int slotCount, IReadOnlyDictionary<int, SlotEffect> special)
    {
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (!special.ContainsKey(slot))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>The saved form: "netId:slot=Effect,slot=Effect;netId:...", players and slots in ascending order.</summary>
    public static string Encode(IReadOnlyDictionary<ulong, IReadOnlyDictionary<int, SlotEffect>> slots) =>
        string.Join(";", slots
            .Where(player => player.Value.Count > 0)
            .OrderBy(player => player.Key)
            .Select(player => Inv(player.Key) + ":" + string.Join(",", player.Value
                .OrderBy(slot => slot.Key)
                .Select(slot => Inv(slot.Key) + "=" + slot.Value))));

    /// <summary>Reads <see cref="Encode"/>'s form; malformed parts and unknown effect names are skipped.</summary>
    public static Dictionary<ulong, IReadOnlyDictionary<int, SlotEffect>> Decode(string? text)
    {
        var result = new Dictionary<ulong, IReadOnlyDictionary<int, SlotEffect>>();
        foreach (string part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(part[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out ulong playerId))
            {
                continue;
            }

            var playerSlots = new Dictionary<int, SlotEffect>();
            foreach (string entry in part[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split('=');
                if (pair.Length == 2
                    && int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out int slot)
                    && Enum.TryParse(pair[1], ignoreCase: false, out SlotEffect effect)
                    && Enum.IsDefined(effect)
                    && !int.TryParse(pair[1], out _))
                {
                    playerSlots[slot] = effect;
                }
            }

            if (playerSlots.Count > 0)
            {
                result[playerId] = playerSlots;
            }
        }

        return result;
    }

    /// <summary>
    /// Whether the enemy at <paramref name="enemyIndex"/> brews on round <paramref name="round"/>: enemies take turns, the first
    /// (index 0) on odd rounds and the next on even rounds, so about half brew each turn.
    /// </summary>
    public static bool Brews(int enemyIndex, int round) => (enemyIndex + round) % 2 == 1;

    /// <summary>The potion an enemy brews this round, each equally likely.</summary>
    public static EnemyPotion PickEnemyPotion(ulong runSeed, string locationKey, string enemyKey, int round)
    {
        var rng = ZoneRandom.ForStream(runSeed, "fermentory.brew:" + locationKey + ":" + enemyKey + ":" + Inv(round));
        return AllEnemyPotions[rng.NextInt(AllEnemyPotions.Count)];
    }

    /// <summary>
    /// An enemy potion's number: the Act 1 value × 1.5 in Act 2 and × 2 from Act 3 on, rounded half up. Block and Regen are
    /// multiplied by the player count; Weak and Vulnerable are always 1; Blood heals a flat 15% of Max HP.
    /// </summary>
    public static int Amount(EnemyPotion potion, int actIndex, int playerCount)
    {
        decimal scale = actIndex <= 0 ? 1m : actIndex == 1 ? 1.5m : 2m;
        int players = Math.Max(1, playerCount);
        int Scaled(int act1) => (int)Math.Round(act1 * scale, MidpointRounding.AwayFromZero);
        return potion switch
        {
            EnemyPotion.Strength => Scaled(2),
            EnemyPotion.Block => Scaled(10) * players,
            EnemyPotion.Regen => Scaled(4) * players,
            EnemyPotion.LiquidBronze => Scaled(3),
            EnemyPotion.Weak => 1,
            EnemyPotion.Vulnerable => 1,
            EnemyPotion.Poison => Scaled(5),
            EnemyPotion.Fire => Scaled(8),
            EnemyPotion.Blood => BloodHealPercent,
            _ => 0,
        };
    }

    /// <summary>HP a Blood Potion heals an enemy: 15% of its Max HP, rounded up.</summary>
    public static int BloodHeal(int maxHp) => (int)Math.Ceiling(Math.Max(0, maxHp) * BloodHealPercent / 100m);

    /// <summary>
    /// The extra reward of a Fermentory fight, each equally likely. When no slot can become special, a special slot roll becomes
    /// one of the other two instead, so the reward is never empty.
    /// </summary>
    public static FightReward PickFightReward(ulong runSeed, string locationKey, ulong playerId, bool canGainSpecial)
    {
        var rng = ZoneRandom.ForStream(runSeed, "fermentory.reward:" + locationKey + ":" + Inv(playerId));
        var reward = (FightReward)rng.NextInt(3);
        return reward == FightReward.SpecialSlot && !canGainSpecial ? (FightReward)rng.NextInt(2) : reward;
    }

    private static string Inv(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
