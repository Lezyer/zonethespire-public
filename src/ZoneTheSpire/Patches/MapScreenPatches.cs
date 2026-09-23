using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Patches;

/// <summary>SetMap rebuilds every point node; wait one frame so their sizes/positions are final.</summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class NMapScreenSetMapPatch
{
    private static void Postfix(NMapScreen __instance) => ZoneOverlay.RefreshDeferred(__instance);
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class NMapScreenOpenPatch
{
    private static void Postfix(NMapScreen __instance) => ZoneOverlay.RefreshDeferred(__instance);
}

/// <summary>Called when travel/votes change point state; refresh passed-zone fading.</summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.RefreshAllPointVisuals))]
internal static class NMapScreenRefreshAllPointVisualsPatch
{
    private static void Postfix(NMapScreen __instance) => ZoneOverlay.Refresh(__instance);
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
internal static class NMapScreenClosePatch
{
    private static void Postfix(NMapScreen __instance) => ZoneOverlay.Remove(__instance);
}
