using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Adds the hidden modifier to every run. Runs identically on every multiplayer peer (each peer builds its own
/// RunState for new runs), and appends at the end so hook order matches everywhere.
/// </summary>
internal static class RunModifierInjection
{
    internal static void EnsureModifier(RunState? state)
    {
        if (state == null)
        {
            return;
        }

        try
        {
            if (!state.Modifiers.Any(modifier => modifier is ZoneTheSpireModifier))
            {
                state.AddModifierDebug(ModelDb.Modifier<ZoneTheSpireModifier>().ToMutable());
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Zone the Spire run modifier: {ex}");
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class RunStateCreateForNewRunPatch
{
    private static void Postfix(RunState __result)
    {
        RunLifecycle.Reset();
        RunModifierInjection.EnsureModifier(__result);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class RunStateFromSerializablePatch
{
    private static void Postfix(RunState __result)
    {
        RunLifecycle.Reset();
        RunModifierInjection.EnsureModifier(__result);
    }
}

/// <summary>A run ends (win, loss, abandon, quit, disconnect): the mod's per-run state goes with it.</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class RunManagerCleanUpPatch
{
    private static void Prefix() => RunLifecycle.Reset();
}
