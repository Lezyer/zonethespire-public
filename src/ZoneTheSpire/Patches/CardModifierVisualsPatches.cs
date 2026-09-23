using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Keeps the zone card modifier overlays (Phantasm-Haunted, Wriggling) in sync whenever a card node reloads its model
/// (NCard.Reload) or refreshes its visuals (NCard.UpdateVisuals), wherever the card is shown.
/// </summary>
[HarmonyPatch]
internal static class CardModifierVisualsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // UpdateEnergyCostColor re-colours the cost on its own (e.g. when energy changes in combat), so re-tint after it too.
        foreach (string name in new[] { "Reload", nameof(NCard.UpdateVisuals), "UpdateEnergyCostColor" })
        {
            if (AccessTools.DeclaredMethod(typeof(NCard), name) is { } method)
            {
                yield return method;
            }
            else
            {
                Log.Warn($"NCard.{name} not found: card modifier overlays won't refresh after it.");
            }
        }
    }

    private static void Postfix(NCard __instance) => CardModifierVisuals.Refresh(__instance);
}
