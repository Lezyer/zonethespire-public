using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Patches;

/// <summary>NNormalMapPoint calls base.OnFocus, so patching the base covers normal map nodes.</summary>
[HarmonyPatch(typeof(NMapPoint), "OnFocus")]
internal static class NMapPointOnFocusPatch
{
    private static void Postfix(NMapPoint __instance) => ZoneNodeTooltip.Show(__instance);
}

/// <summary>
/// Hides the zone tip when a node loses hover or is pressed. NMapPoint.OnUnfocus is a two-line method that the JIT can
/// inline into the subclasses' base.OnUnfocus() calls, which bypasses a postfix on the base. The subclass overrides are
/// only reached through virtual dispatch, so they are patched as well. Hide is idempotent.
/// </summary>
[HarmonyPatch]
internal static class NMapPointHideTipPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        int found = 0;
        foreach (System.Type type in new[] { typeof(NMapPoint), typeof(NNormalMapPoint), typeof(NBossMapPoint), typeof(NAncientMapPoint) })
        {
            foreach (string name in new[] { "OnUnfocus", "OnPress" })
            {
                if (AccessTools.DeclaredMethod(type, name) is { } method)
                {
                    found++;
                    yield return method;
                }
            }
        }

        if (found == 0)
        {
            Log.Warn("No map point OnUnfocus/OnPress methods found: zone node tooltips won't hide on their own.");
        }
    }

    private static void Postfix(NMapPoint __instance) => ZoneNodeTooltip.Hide(__instance);
}
