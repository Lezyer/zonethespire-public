using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Campfire options have no hover tips of their own (their description shows in the room's banner). A focused option whose
/// description names mod keywords gets a tip for each beside its button, nested like every other mod tip. Local UI only.
/// </summary>
internal static class CampfireTooltips
{
    /// <summary>
    /// With a controller the tips can be locked and followed like others (hold Peek). False shows them as plain tips instead,
    /// which can't be selected.
    /// </summary>
    public static readonly bool SelectableWithController = true;

    public static void Show(NRestSiteButton button)
    {
        try
        {
            if (!NestedTooltips.Enabled || !GodotObject.IsInstanceValid(button) || button.Option is not { } option)
            {
                return;
            }

            List<IHoverTip> tips = NestedTooltips.TipsNamedIn(option.Description.GetFormattedText(), option.Title.GetFormattedText());
            if (tips.Count == 0)
            {
                return;
            }

            NHoverTipSet.Remove(button);
            NHoverTipSet? set = NestedTooltips.UsingController && !SelectableWithController
                ? NestedTooltips.CreatePlain(button, tips)
                : NHoverTipSet.CreateAndShow(button, tips);
            set?.SetAlignment(button, HoverTip.GetHoverTipAlignment(button));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show campfire option tips: {ex}");
        }
    }

    public static void Hide(NRestSiteButton button)
    {
        try
        {
            NHoverTipSet.Remove(button);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to hide campfire option tips: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NRestSiteButton), "OnFocus")]
internal static class CampfireTooltipFocusPatch
{
    private static void Postfix(NRestSiteButton __instance) => CampfireTooltips.Show(__instance);
}

[HarmonyPatch]
internal static class CampfireTooltipUnfocusPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(NRestSiteButton), "OnUnfocus");
        yield return AccessTools.DeclaredMethod(typeof(NRestSiteButton), "OnRelease");
    }

    private static void Postfix(NRestSiteButton __instance) => CampfireTooltips.Hide(__instance);
}
