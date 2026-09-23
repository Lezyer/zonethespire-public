using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Run.ZoneEvents;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Intercepts event selection only after a question-mark room has resolved to RoomType.Event. Returning true leaves the
/// vanilla event grab bag completely untouched when no eligible zone event is rolled.
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEvent))]
internal static class ZoneEventPullPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(RunState runState, ref EventModel __result)
    {
        int? originalChance = null;
        try
        {
            originalChance = ZoneEventSystem.CurrentEligibleChance(runState);
            EventModel? zoneEvent = ZoneEventSystem.TryPull(runState);
            if (zoneEvent == null)
            {
                return true;
            }

            __result = zoneEvent;
            return false;
        }
        catch (Exception ex)
        {
            // A framework or third-party event failure must degrade to vanilla selection instead of blocking the room.
            if (originalChance is { } chance)
            {
                ZoneEventSystem.RecordNonZoneEvent(runState, chance);
            }

            Log.Warn($"Failed to select a zone event; using the vanilla event roll: {ex}");
            return true;
        }
    }
}
