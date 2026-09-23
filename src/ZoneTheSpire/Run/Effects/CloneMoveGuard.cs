using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Clones never pick a move with a summon intent: they repeat their last performed move, or before their first move skip to
/// what normally follows the summon. Uses no RNG, so every peer makes the same choice.
/// </summary>
internal static class CloneMoveGuard
{
    private const int MaxBranchDepth = 8;

    private static readonly ConditionalWeakTable<MonsterModel, MoveState> LastPerformed = new();

    [ThreadStatic]
    private static bool _replacing;

    public static void RecordPerformed(MonsterModel monster)
    {
        if (CloneIdentity.IsClone(monster.Creature))
        {
            LastPerformed.AddOrUpdate(monster, monster.NextMove);
        }
    }

    public static void ReplaceSummon(MonsterModel monster)
    {
        if (_replacing)
        {
            return;
        }

        try
        {
            MonsterMoveStateMachine? machine = monster.MoveStateMachine;
            if (machine == null || !CloneIdentity.IsClone(monster.Creature))
            {
                return;
            }

            MoveState next = monster.NextMove;
            if (!Summons(next))
            {
                return;
            }

            LastPerformed.TryGetValue(monster, out MoveState? last);
            MoveState? replacement = SummonGuard.ChooseReplacement(
                next,
                last,
                FollowUps(next, machine, monster.Creature),
                machine.States.Values.OfType<MoveState>(),
                Summons);
            if (replacement == null)
            {
                Log.Warn($"Mirrored clone {monster.Id.Entry} has only summoning moves; its spawns will be blocked instead.");
                return;
            }

            _replacing = true;
            monster.SetMoveImmediate(replacement, forceTransition: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to replace a mirrored clone's summon move: {ex}");
        }
        finally
        {
            _replacing = false;
        }
    }

    private static bool Summons(MoveState move) => move.Intents.Any(intent => intent.IntentType == IntentType.Summon);

    /// <summary>Moves reachable from the move's follow-up without rolling RNG, in branch order.</summary>
    private static List<MoveState> FollowUps(MoveState move, MonsterMoveStateMachine machine, Creature owner)
    {
        var moves = new List<MoveState>();
        MonsterState? followUp = move.FollowUpState;
        if (followUp == null && move.FollowUpStateId != null)
        {
            machine.States.TryGetValue(move.FollowUpStateId, out followUp);
        }

        Collect(followUp, machine, owner, moves, 0);
        return moves;
    }

    private static void Collect(MonsterState? state, MonsterMoveStateMachine machine, Creature owner, List<MoveState> moves, int depth)
    {
        if (state == null || depth > MaxBranchDepth)
        {
            return;
        }

        switch (state)
        {
            case MoveState moveState:
                moves.Add(moveState);
                break;
            case ConditionalBranchState conditional:
                string? chosen = null;
                try
                {
                    chosen = conditional.GetNextState(owner, null!);
                }
                catch (Exception)
                {
                    // No branch applies right now; fall through to the machine's other moves.
                }

                if (chosen != null && machine.States.TryGetValue(chosen, out MonsterState? chosenState))
                {
                    Collect(chosenState, machine, owner, moves, depth + 1);
                }

                break;
            case RandomBranchState random:
                foreach (RandomBranchState.StateWeight branch in random.States)
                {
                    float weight;
                    try
                    {
                        weight = branch.GetWeight();
                    }
                    catch (Exception)
                    {
                        weight = 0f;
                    }

                    if (weight > 0f && machine.States.TryGetValue(branch.stateId, out MonsterState? branchState))
                    {
                        Collect(branchState, machine, owner, moves, depth + 1);
                    }
                }

                break;
        }
    }
}
