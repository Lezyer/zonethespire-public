using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Catch-all: skips adding an enemy while a Mirrorlands clone is acting. Vanilla takes the same early return when combat is
/// no longer live, so callers already cope with the creature not joining. A clone being spawned is always allowed.
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Add), new[] { typeof(Creature) })]
internal static class CreatureCmdAddBlockPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    {
        try
        {
            Creature? clone = CloneSpawnScope.Current;
            if (clone == null || creature.Side != CombatSide.Enemy || CloneRegistry.IsPending(creature))
            {
                return true;
            }

            __result = Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Mirrored clone spawn guard", ex);
            return true;
        }
    }
}

/// <summary>A clone's move runs inside its spawn scope and is remembered as the move to repeat instead of summoning.</summary>
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class CloneMovePerformPatch
{
    private static void Prefix(MonsterModel __instance, out CloneSpawnScope.Token __state)
    {
        __state = default;
        try
        {
            CloneMoveGuard.RecordPerformed(__instance);
            __state = CloneSpawnScope.EnterIfClone(__instance.Creature);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to scope a mirrored clone's move: {ex}");
        }
    }

    private static Exception? Finalizer(Exception? __exception, CloneSpawnScope.Token __state)
    {
        CloneSpawnScope.Exit(__state);
        return __exception;
    }
}

/// <summary>Every scanned spawn site (summon moves, death triggers) runs inside its owner's spawn scope when the owner is a clone.</summary>
[HarmonyPatch]
internal static class CloneSpawnSitePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        List<MethodBase> sites = SpawnSiteScanner.Find();
        if (sites.Count == 0)
        {
            Log.Warn("No creature spawn sites found: mirrored clones may summon.");
        }
        return sites;
    }

    private static void Prefix(object __instance, out CloneSpawnScope.Token __state)
    {
        __state = default;
        try
        {
            Creature? owner = __instance switch
            {
                MonsterModel monster => monster.Creature,
                PowerModel power => power.Owner,
                _ => null,
            };
            __state = CloneSpawnScope.EnterIfClone(owner);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to scope a mirrored clone's spawn site: {ex}");
        }
    }

    private static Exception? Finalizer(Exception? __exception, CloneSpawnScope.Token __state)
    {
        CloneSpawnScope.Exit(__state);
        return __exception;
    }
}

/// <summary>
/// Swaps a clone's summon move after every way a move gets chosen. PrepareForNextTurn is patched as well because RollMove
/// is a one-liner the JIT may inline into it. Replacing is idempotent.
/// </summary>
[HarmonyPatch]
internal static class CloneSummonMovePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.RollMove));
        yield return AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate));
        yield return AccessTools.Method(typeof(Creature), nameof(Creature.PrepareForNextTurn));
    }

    private static void Postfix(object __instance)
    {
        MonsterModel? monster = __instance switch
        {
            MonsterModel model => model,
            Creature creature => creature.Monster,
            _ => null,
        };
        if (monster != null)
        {
            CloneMoveGuard.ReplaceSummon(monster);
        }
    }
}

/// <summary>A clone gremlin's Surprise can't spawn the Fat Gremlin that returns stolen gold, so the gold is returned directly.</summary>
[HarmonyPatch(typeof(SurprisePower), nameof(SurprisePower.AfterDeath))]
internal static class CloneSurpriseGoldPatch
{
    private static bool Prefix(SurprisePower __instance, Creature target, bool wasRemovalPrevented, ref Task __result)
    {
        try
        {
            if (wasRemovalPrevented || __instance.Owner != target || !CloneIdentity.IsClone(target))
            {
                return true;
            }

            CloneLoot.ReturnStolenGold(target);
            __result = Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to handle a mirrored clone's Surprise: {ex}");
            return true;
        }
    }
}
