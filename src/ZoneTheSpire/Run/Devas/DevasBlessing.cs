using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// Deva's Blessing (see <see cref="DevasBlessingPower"/>): which enemies get it, the Decimillipede group case, and the death
/// after a blessed enemy's next action. Enemies whose own powers already bring them back or replace them on death are left
/// alone: Adaptable (Test Subject), Illusion, Steam Eruption (Waterfall Giant), Infested, Stock and Surprise. Decimillipede
/// segments (Reattach) keep reviving each other as usual; the blessing only steps in when the last segment would die. Once a
/// blessing has been spent, anything that revives that enemy afterwards is undone at once (KillIfRevivedAfterBlessing), except
/// for those same segments.
/// </summary>
internal static class DevasBlessing
{
    /// <summary>Vanilla powers that make an enemy revive, split or be replaced on death.</summary>
    private static readonly Type[] RevivingPowers =
    {
        typeof(AdaptablePower),
        typeof(IllusionPower),
        typeof(SteamEruptionPower),
        typeof(InfestedPower),
        typeof(StockPower),
        typeof(SurprisePower),
    };

    private static readonly string[] NonAttackSegmentMoves = { "DEAD_MOVE", "REATTACH_MOVE" };

    /// <summary>Whether the creature comes back from death on its own in vanilla (and so never gets the blessing's effect).</summary>
    public static bool RevivesOnItsOwn(Creature creature) =>
        creature.Powers.Any(power => RevivingPowers.Contains(power.GetType()));

    /// <summary>Gives an enemy Deva's Blessing (once), unless it revives or splits on its own.</summary>
    public static async Task TryGive(Creature creature)
    {
        try
        {
            if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive
                || creature.HasPower<DevasBlessingPower>() || RevivesOnItsOwn(creature))
            {
                return;
            }

            await PowerCmd.Apply<DevasBlessingPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to give an enemy Deva's Blessing: {ex}");
        }
    }

    /// <summary>
    /// Whether this (unblessed) enemy's death is prevented: not when it revives on its own (checked again here, as such powers
    /// may be applied after the blessing), and for a Decimillipede segment only when every other segment is already dead.
    /// </summary>
    public static bool ShouldPrevent(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !CombatManager.Instance.IsInProgress || RevivesOnItsOwn(creature))
        {
            return false;
        }

        return !creature.HasPower<ReattachPower>() || OtherSegments(creature).All(segment => segment.IsDead);
    }

    /// <summary>The enemies that live on together: every Decimillipede segment, or just the creature.</summary>
    public static IReadOnlyList<Creature> GroupOf(Creature creature) =>
        creature.HasPower<ReattachPower>()
            ? new[] { creature }.Concat(OtherSegments(creature)).ToList()
            : new[] { creature };

    private static IEnumerable<Creature> OtherSegments(Creature creature) =>
        creature.CombatState?.GetTeammatesOf(creature).Where(other => other != creature && other.HasPower<ReattachPower>())
        ?? Enumerable.Empty<Creature>();

    /// <summary>
    /// A dead Decimillipede segment the group blessing brought back (it already has HP): it reattaches (visuals, targetable
    /// again) and gets an attack as its next move instead of its dead/reattach moves. <paramref name="index"/> spreads the
    /// moves across the segments; it is the same on every peer.
    /// </summary>
    public static async Task ReviveSegment(Creature segment, int index)
    {
        try
        {
            if (segment.GetPower<ReattachPower>() is { } reattach)
            {
                await reattach.DoReattach();
            }

            if (segment.Monster is { MoveStateMachine: { } machine } monster)
            {
                List<MoveState> attacks = machine.States.Values
                    .OfType<MoveState>()
                    .Where(state => !NonAttackSegmentMoves.Contains(state.Id))
                    .OrderBy(state => state.Id, StringComparer.Ordinal)
                    .ToList();
                if (attacks.Count > 0)
                {
                    monster.SetMoveImmediate(attacks[index % attacks.Count], forceTransition: true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to bring back a Decimillipede segment: {ex}");
        }
    }

    /// <summary>Enemies whose blessing is spent: they lived on, acted, and died. It outlives their powers and removal.</summary>
    private static readonly ConditionalWeakTable<Creature, object> Spent = new();

    /// <summary>Remembers that this enemy has already had its extra action (called for every death of a blessed enemy).</summary>
    public static void MarkSpentIfBlessed(Creature creature)
    {
        if (creature.GetPower<DevasBlessingPower>() is { IsBlessed: true })
        {
            Spent.AddOrUpdate(creature, creature);
        }
    }

    /// <summary>
    /// Whether this enemy is back on its feet after it had already spent its blessing. Decimillipede segments are left out, since
    /// reviving each other is their own mechanic.
    /// </summary>
    public static bool IsRevivedAfterBlessing(Creature creature) =>
        Spent.TryGetValue(creature, out _)
        && creature.IsAlive
        && !creature.HasPower<ReattachPower>()
        && CombatManager.Instance.IsInProgress;

    /// <summary>
    /// A blessing is spent once, whatever brings the enemy back. Anything that revives it later (its own powers, a card, a relic,
    /// another mod) is undone at once: it dies again the moment it has HP. Decimillipede segments are left out, since reviving
    /// each other is their own mechanic.
    /// </summary>
    public static async Task KillIfRevivedAfterBlessing(Creature creature)
    {
        try
        {
            if (!IsRevivedAfterBlessing(creature))
            {
                return;
            }

            await CreatureCmd.Kill(creature, force: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to end a revived enemy: {ex}");
        }
    }

    /// <summary>Whether the enemy's next turn is its blessed last action (it was blessed before the turn started).</summary>
    public static bool IsDueToDie(Creature creature) =>
        creature.GetPower<DevasBlessingPower>() is { IsBlessed: true }
        && creature.IsAlive
        && creature.Monster is { SpawnedThisTurn: false };

    /// <summary>After a blessed enemy's action: it dies.</summary>
    public static async Task AfterTurn(Task turn, Creature creature, bool due)
    {
        await turn;
        try
        {
            if (!due || !creature.IsAlive || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            await CreatureCmd.Kill(creature);
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to end a blessed enemy: {ex}");
        }
    }
}
