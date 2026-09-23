using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Records where a creature's node stood as the combat room lets it go. Zones that put something in a dead enemy's place
/// (Infestation's Wrigglers, Phantasmal Tombs' spectres) read the node at death, but a Doom death removes the node and queues
/// it free before the kill, so the node can be gone by then. This runs at the removal itself, the last moment the node is
/// certainly still there. Local rendering only.
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
internal static class CreatureDeathSpotPatch
{
    private static void Prefix(NCreature node)
    {
        try
        {
            CreatureDeathSpots.Record(node);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to record a leaving creature's position: {ex.Message}");
        }
    }
}
