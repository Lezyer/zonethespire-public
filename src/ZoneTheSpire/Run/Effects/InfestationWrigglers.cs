using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Infestation: a Wriggler crawls out where a non-minion enemy died. Runs inside the synced death hooks on every peer; the
/// per-combat state (spawn order, which enemies already released a Wriggler) is identical everywhere.
/// </summary>
internal static class InfestationWrigglers
{
    /// <summary>Hidden for this long after spawning (normal / fast game speed; instant skips it).</summary>
    private const float RevealDelayNormal = 0.25f;
    private const float RevealDelayFast = 0.1f;
    private const int MaxLayoutRetries = 3;

    private sealed class SpawnInfo
    {
        public SpawnInfo(int index) => Index = index;

        public int Index { get; }
    }

    private sealed class Counter
    {
        public int Next;
    }

    /// <summary>Where a dead enemy stood, and its node when that is still around (a Doom death frees it early).</summary>
    private sealed record DeathSpot(NCreature? Node, Vector2 Position);

    /// <summary>Wrigglers spawned by the Infestation, with their spawn order in the combat.</summary>
    private static readonly ConditionalWeakTable<Creature, SpawnInfo> Spawned = new();

    /// <summary>Enemies that already released a Wriggler (reviving enemies release only once).</summary>
    private static readonly ConditionalWeakTable<Creature, object> Released = new();

    private static readonly ConditionalWeakTable<ICombatState, Counter> Counters = new();

    private static readonly ConditionalWeakTable<Creature, DeathSpot> DeathSpots = new();

    public static int? SpawnIndexOf(Creature creature) => Spawned.TryGetValue(creature, out SpawnInfo? info) ? info.Index : null;

    public static bool ShouldSpawnFor(Creature dead) =>
        InfestationRules.ShouldSpawnWriggler(
            isEnemyMonster: dead.Side == CombatSide.Enemy && dead.IsMonster,
            isMinion: dead.IsSecondaryEnemy,
            isPhrogParasite: dead.Monster is PhrogParasite,
            spawnedByInfestation: Spawned.TryGetValue(dead, out _),
            alreadyReleased: Released.TryGetValue(dead, out _));

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
            // it let the node go (CreatureDeathSpots); the Wriggler still crawls out in the right place, with no body to fade.
            if (CreatureDeathSpots.LastPosition(creature) is { } position)
            {
                DeathSpots.AddOrUpdate(creature, new DeathSpot(null, position));
                return;
            }

            Log.Warn($"Infestation: {creature.Monster?.Id.Entry ?? "an enemy"} died with no node and no recorded spot; its Wriggler crawls out where the game put it.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to record where an Infestation enemy died: {ex}");
        }
    }

    public static async Task SpawnFor(Creature dead)
    {
        DeathSpots.TryGetValue(dead, out DeathSpot? deathSpot);
        DeathSpots.Remove(dead);
        ICombatState? state = dead.CombatState;
        if (state == null)
        {
            return;
        }

        Released.AddOrUpdate(dead, new object());
        // Reviving enemies (Decimillipede segments, modded revivers) keep their body in combat after death.
        bool keepsBody = !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(state, dead);

        // Vanilla kills a leader's remaining minions after this hook, but only if no primary enemy is left. The Wriggler is
        // a primary enemy, so do that cleanup first to keep the vanilla behaviour.
        List<Creature> teammates = state.GetTeammatesOf(dead).Where(teammate => teammate != dead && teammate.IsAlive).ToList();
        if (teammates.Count > 0 && teammates.All(teammate => teammate.IsSecondaryEnemy))
        {
            await CreatureCmd.Kill(teammates);
        }

        var model = (Wriggler)ModelDb.Monster<Wriggler>().ToMutable();
        model.StartStunned = true;
        Creature wriggler = state.CreateCreature(model, CombatSide.Enemy, null);
        Counter counter = Counters.GetOrCreateValue(state);
        Spawned.AddOrUpdate(wriggler, new SpawnInfo(counter.Next++));
        await CreatureCmd.Add(wriggler);
        // Tougher Infestation Wrigglers: +15% / +30% / +60% Max HP in acts 1 / 2 / 3, on top of the game's own scaling.
        int actIndex = state.RunState.CurrentActIndex;
        int boostedHp = InfestationRules.BoostedWrigglerMaxHp(wriggler.MaxHp, actIndex);
        await CreatureCmd.SetMaxAndCurrentHp(wriggler, boostedHp);

        Show(dead, wriggler, deathSpot, keepsBody, state);
    }

    /// <summary>
    /// Visual only. The Wriggler appears where the enemy stood: behind its fading body when the enemy leaves combat, on top
    /// of its body when the enemy revives (Decimillipede segments, Darklings), where each stays individually selectable.
    /// Revealed after a short delay.
    /// </summary>
    private static void Show(Creature dead, Creature wriggler, DeathSpot? deathSpot, bool keepsBody, ICombatState state)
    {
        wriggler.SetNodeVisible(false);
        TaskHelper.RunSafely(RevealAfterDelay(wriggler));
        Place(dead, wriggler, deathSpot, keepsBody, state, 0);
    }

    private static void Place(Creature dead, Creature wriggler, DeathSpot? deathSpot, bool keepsBody, ICombatState state, int attempt)
    {
        try
        {
            if (NCombatRoom.Instance?.GetCreatureNode(wriggler) is not NCreature node)
            {
                return;
            }

            if (!node.IsInsideTree())
            {
                // CreatureCmd.Add uses AddChildSafely, which defers adding the node while the container is busy (e.g. two
                // Decimillipede segments dying in one action). Deferred calls run in order, so this runs after that add.
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Place(dead, wriggler, deathSpot, keepsBody, state, attempt + 1)).CallDeferred();
                }
                else
                {
                    Log.Warn($"Infestation Wriggler: node still not in the scene after {attempt} deferred layouts; left where it is.");
                }

                return;
            }

            if (deathSpot == null)
            {
                return;
            }

            node.Position = deathSpot.Position;
            NCreature? dying = deathSpot.Node;
            // A leaving body fades out over the Wriggler; a body that stays (reviving enemy) keeps it drawn on top.
            if (!keepsBody && dying != null && GodotObject.IsInstanceValid(dying) && dying.IsInsideTree() && dying.GetParent() == node.GetParent())
            {
                node.GetParent().MoveChild(node, dying.GetIndex());
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to position an Infestation Wriggler: {ex}");
        }
    }

    private static async Task RevealAfterDelay(Creature wriggler)
    {
        try
        {
            await Cmd.CustomScaledWait(RevealDelayFast, RevealDelayNormal, ignoreCombatEnd: true);
        }
        finally
        {
            wriggler.SetNodeVisible(true);
        }
    }
}
