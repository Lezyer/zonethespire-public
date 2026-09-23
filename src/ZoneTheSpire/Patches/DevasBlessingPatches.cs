using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Deva's Blessing: an enemy that was already living on when its turn started dies right after that turn's action. Whether it
/// is due is read before the turn (an enemy blessed during its own action, e.g. by Thorns, gets its next turn), and the kill
/// is chained onto the turn's task, so the enemy turn waits for it. Runs in the synced enemy turn on every peer.
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
internal static class DevasBlessingTakeTurnPatch
{
    private static bool Prefix(Creature __instance, out bool __state, ref Task __result)
    {
        __state = false;
        try
        {
            // Safety net for a revive that never touched the HP hooks: it takes no turn, it just dies.
            if (DevasBlessing.IsRevivedAfterBlessing(__instance))
            {
                __result = DevasBlessing.KillIfRevivedAfterBlessing(__instance);
                return false;
            }

            __state = DevasBlessing.IsDueToDie(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to check an enemy's turn: {ex.Message}");
        }

        return true;
    }

    private static void Postfix(Creature __instance, bool __state, ref Task __result)
    {
        if (__state)
        {
            __result = DevasBlessing.AfterTurn(__result, __instance, due: true);
        }
    }
}
