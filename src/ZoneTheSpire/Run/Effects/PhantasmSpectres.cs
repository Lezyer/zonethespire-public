using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Phantasmal Tombs deaths. A Phantasm whose body leaves combat rises as a ghostly copy (fresh copy of its monster, 3 / 5 / 7
/// HP by act, 99 Intangible, no Phantasm, never summons, leaves nothing when it dies). A Phantasm whose body stays to revive
/// (Decimillipede segments, modded Darklings) instead rises straight away the first time, at 20% HP with 1 Intangible. Runs
/// inside the synced death hooks on every peer, after the enemy's own powers (e.g. Reattach) have handled the death.
/// </summary>
internal static class PhantasmSpectres
{
    private const int MaxLayoutRetries = 3;
    private const string ReattachMoveId = "REATTACH_MOVE";

    private static readonly object Marker = new();

    /// <summary>Where a dead enemy stood, and its node when that is still around (a Doom death frees it early).</summary>
    private sealed record DeathSpot(NCreature? Node, Vector2 Position);

    private static readonly ConditionalWeakTable<Creature, object> Spectres = new();
    private static readonly ConditionalWeakTable<Creature, object> Revived = new();
    private static readonly ConditionalWeakTable<Creature, DeathSpot> DeathSpots = new();

    public static bool IsSpectre(Creature creature) => Spectres.TryGetValue(creature, out _);

    public static async Task GivePhantasm(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || IsSpectre(creature) || creature.HasPower<PhantasmPower>())
        {
            return;
        }

        await PowerCmd.Apply<PhantasmPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
    }

    public static void RememberDeathPosition(Creature creature)
    {
        try
        {
            if (creature.Side != CombatSide.Enemy)
            {
                return;
            }

            // Also found mid-removal: a Doom death removes the node from the room before the death hooks run.
            if (CreatureNodeLookup.Find(creature) is { } node)
            {
                DeathSpots.AddOrUpdate(creature, new DeathSpot(node, node.Position));
                return;
            }

            // A Doom death also queues the node free, so by now it can be gone entirely. The room recorded where it stood as
            // it let the node go (CreatureDeathSpots), which is enough to raise the spectre in the right place.
            if (CreatureDeathSpots.LastPosition(creature) is { } position)
            {
                DeathSpots.AddOrUpdate(creature, new DeathSpot(null, position));
                return;
            }

            Log.Warn($"Phantasmal Tombs: {creature.Monster?.Id.Entry ?? "an enemy"} died with no node and no recorded spot; its spectre rises where the game put it.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to record where a Phantasm died: {ex}");
        }
    }

    public static async Task HandleDeath(Creature dead)
    {
        DeathSpots.TryGetValue(dead, out DeathSpot? deathSpot);
        DeathSpots.Remove(dead);
        ICombatState? state = dead.CombatState;
        if (state == null)
        {
            return;
        }

        bool enemyMonster = dead.Side == CombatSide.Enemy && dead.IsMonster;
        bool hasPhantasm = dead.HasPower<PhantasmPower>();
        bool keepsBody = !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, dead);
        if (PhantasmalRules.ShouldReviveOnce(enemyMonster, hasPhantasm, IsSpectre(dead), keepsBody, Revived.TryGetValue(dead, out _)))
        {
            await Revive(dead);
        }
        else if (PhantasmalRules.ShouldSpawnCopy(enemyMonster, hasPhantasm, dead.IsSecondaryEnemy, IsSpectre(dead), keepsBody))
        {
            await SpawnSpectre(dead, state, deathSpot);
        }
    }

    private static async Task Revive(Creature dead)
    {
        Revived.AddOrUpdate(dead, Marker);
        int reviveHp = PhantasmalRules.ReviveHp(dead.MaxHp);
        ReattachPower? reattach = dead.Powers.OfType<ReattachPower>().FirstOrDefault();
        if (reattach != null)
        {
            // Reattach already marked the segment as reviving. Reattach it now (it stays dead when every other segment is
            // dead: the fight is ending), then set its HP and send it back to its normal attacks instead of the Reattach move.
            await reattach.DoReattach();
            if (dead.IsDead)
            {
                return;
            }

            await CreatureCmd.SetCurrentHp(dead, reviveHp);
            if (dead.Monster is DecimillipedeSegment segment && segment.MoveStateMachine is { } machine
                && machine.States.Values.OfType<MoveState>().FirstOrDefault(move => move != segment.DeadState && move.Id != ReattachMoveId) is { } attack)
            {
                segment.SetMoveImmediate(attack, forceTransition: true);
            }
        }
        else
        {
            await CreatureCmd.Heal(dead, reviveHp);
            if (dead.IsDead)
            {
                return;
            }
        }

        await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), dead, PhantasmalRules.ReviveIntangible, null, null);
        dead.GetPower<PhantasmPower>()?.MarkRisen();
    }

    private static async Task SpawnSpectre(Creature dead, ICombatState state, DeathSpot? deathSpot)
    {
        MonsterModel? monster = dead.Monster;
        if (monster == null)
        {
            return;
        }

        // Vanilla kills a leader's remaining minions after this hook, but only if no primary enemy is left. The copy is a
        // primary enemy, so do that cleanup first to keep the vanilla behaviour.
        List<Creature> teammates = state.GetTeammatesOf(dead).Where(teammate => teammate != dead && teammate.IsAlive).ToList();
        if (teammates.Count > 0 && teammates.All(teammate => teammate.IsSecondaryEnemy))
        {
            await CreatureCmd.Kill(teammates);
        }

        // Some monsters choose their initial move from their encounter slot. Phantasmal Gardener, for example, has one
        // branch each for first/second/third/fourth and throws while being added if SlotName is null. Preserve the dead
        // creature's slot so the replacement initializes exactly like the original before we adjust its ghost state.
        Creature spectre = state.CreateCreature(monster.CanonicalInstance.ToMutable(), CombatSide.Enemy, dead.SlotName);
        Spectres.AddOrUpdate(spectre, Marker);
        // Treated like a mirrored clone: it never summons and isn't counted as a real enemy by the clone guards.
        CloneIdentity.Mark(spectre);
        CloneRegistry.Add(spectre);
        try
        {
            await CreatureCmd.Add(spectre);
        }
        finally
        {
            CloneRegistry.Remove(spectre);
        }

        int hp = PhantasmalRules.CopyHp(state.RunState.CurrentActIndex, state.Players.Count);
        await CreatureCmd.SetMaxAndCurrentHp(spectre, hp);
        await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), spectre, PhantasmalRules.CopyIntangible, null, null);
        if (spectre.Monster != null)
        {
            CloneMoveGuard.ReplaceSummon(spectre.Monster);
        }

        GhostMaterial.Apply(spectre, GhostStrength.Spectre);
        Place(spectre, deathSpot, 0);
    }

    /// <summary>Visual only: the copy appears where the enemy died, behind its fading body.</summary>
    private static void Place(Creature spectre, DeathSpot? deathSpot, int attempt)
    {
        try
        {
            if (deathSpot == null || NCombatRoom.Instance?.GetCreatureNode(spectre) is not NCreature node)
            {
                return;
            }

            if (!node.IsInsideTree())
            {
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Place(spectre, deathSpot, attempt + 1)).CallDeferred();
                }

                return;
            }

            node.Position = deathSpot.Position;
            NCreature? dying = deathSpot.Node;
            if (dying != null && GodotObject.IsInstanceValid(dying) && dying.IsInsideTree() && dying.GetParent() == node.GetParent())
            {
                node.GetParent().MoveChild(node, dying.GetIndex());
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to position a ghostly copy: {ex}");
        }
    }
}
