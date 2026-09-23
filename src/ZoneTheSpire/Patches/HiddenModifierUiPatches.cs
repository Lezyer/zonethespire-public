using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Patches;

// Vanilla code treats "run has any modifier" as custom-run behaviour. Our modifier is internal plumbing, so each
// patch hides it for the duration of the original method and restores it afterwards (finalizer = always runs).

/// <summary>Neow would replace normal blessings with modifier options.</summary>
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class NeowGenerateInitialOptionsPatch
{
    private static void Prefix(Neow __instance, out IReadOnlyList<ModifierModel>? __state) =>
        __state = HiddenModifier.Hide(__instance.Owner?.RunState);

    private static void Finalizer(Neow __instance, IReadOnlyList<ModifierModel>? __state) =>
        HiddenModifier.Restore(__instance.Owner?.RunState, __state);
}

/// <summary>Neow's opening text would switch to the modifier-run variant.</summary>
[HarmonyPatch(typeof(Neow), nameof(Neow.InitialDescription), MethodType.Getter)]
internal static class NeowInitialDescriptionPatch
{
    private static void Prefix(Neow __instance, out IReadOnlyList<ModifierModel>? __state) =>
        __state = HiddenModifier.Hide(__instance.Owner?.RunState);

    private static void Finalizer(Neow __instance, IReadOnlyList<ModifierModel>? __state) =>
        HiddenModifier.Restore(__instance.Owner?.RunState, __state);
}

/// <summary>The top bar would show an icon for our modifier.</summary>
[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class NTopBarInitializePatch
{
    private static void Prefix(IRunState runState, out IReadOnlyList<ModifierModel>? __state) =>
        __state = HiddenModifier.Hide(runState);

    private static void Finalizer(IRunState runState, IReadOnlyList<ModifierModel>? __state) =>
        HiddenModifier.Restore(runState, __state);
}

/// <summary>The daily load screen indexes a fixed set of modifier slots; an extra modifier would throw.</summary>
[HarmonyPatch(typeof(NDailyRunLoadScreen), "InitializeDisplay")]
internal static class NDailyRunLoadScreenInitializeDisplayPatch
{
    private static void Prefix(LoadRunLobby? ____lobby, out List<SerializableModifier>? __state) =>
        __state = HiddenModifier.HideSerialized(____lobby?.Run);

    private static void Finalizer(LoadRunLobby? ____lobby, List<SerializableModifier>? __state) =>
        HiddenModifier.RestoreSerialized(____lobby?.Run, __state);
}

/// <summary>The custom run load screen would list our modifier.</summary>
[HarmonyPatch(typeof(NCustomRunLoadScreen), nameof(NCustomRunLoadScreen.OnSubmenuOpened))]
internal static class NCustomRunLoadScreenOnSubmenuOpenedPatch
{
    private static void Prefix(LoadRunLobby? ____lobby, out List<SerializableModifier>? __state) =>
        __state = HiddenModifier.HideSerialized(____lobby?.Run);

    private static void Finalizer(LoadRunLobby? ____lobby, List<SerializableModifier>? __state) =>
        HiddenModifier.RestoreSerialized(____lobby?.Run, __state);
}
