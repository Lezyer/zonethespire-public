using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// A Wriggler picks its first move from its encounter slot name (wriggler1-4) and throws when it has none. Wrigglers added
/// without a slot (Infestation spawns, mirrored clones) alternate bite and wriggle by spawn order instead.
/// </summary>
[HarmonyPatch(typeof(ConditionalBranchState), nameof(ConditionalBranchState.GetNextState))]
internal static class SlotlessWrigglerInitialMovePatch
{
    private const string InitMoveId = "INIT_MOVE";

    private static bool Prefix(ConditionalBranchState __instance, Creature __0, ref string __result)
    {
        try
        {
            if (__instance.BranchId != InitMoveId || __0?.Monster is not Wriggler || __0.SlotName != null)
            {
                return true;
            }

            __result = InfestationRules.InitialMoveId(InfestationWrigglers.SpawnIndexOf(__0) ?? 0);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to pick a slotless Wriggler's first move: {ex}");
            return true;
        }
    }
}
