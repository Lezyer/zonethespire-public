using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Hallowed;

namespace ZoneTheSpire.Patches;

/// <summary>A hand-marked card shows the Blasphemous line (cards with the permanent modifier already show it).</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), new[] { typeof(PileType), typeof(Creature) })]
internal static class BlasphemousMarkDescriptionPatch
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            if (BlasphemousMark.IsMarked(__instance) && !BlasphemousModifier.Has(__instance))
            {
                __result = string.IsNullOrEmpty(__result) ? BlasphemousMark.Line : BlasphemousMark.Line + "\n" + __result;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to describe a Blasphemous card: {ex.Message}");
        }
    }
}

/// <summary>Remembers which health bars show which creature, so Hallowed changes can refresh them.</summary>
[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class HallowedHealthBarRegisterPatch
{
    private static void Postfix(NHealthBar __instance, Creature creature) => HallowedHealthBar.Register(creature, __instance);
}

/// <summary>After the game lays out a health bar: place the gold Hallowed segment at the end of the red HP.</summary>
[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class HallowedHealthBarRefreshPatch
{
    private static void Postfix(NHealthBar __instance) => HallowedHealthBar.OnRefreshed(__instance);
}
