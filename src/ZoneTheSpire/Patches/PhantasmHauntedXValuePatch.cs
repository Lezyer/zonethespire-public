using System;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Phantasm-Haunted X-cost cards: X counts 1 higher (the energy is only paid once). Added after every other X modifier
/// (Chemical X and the like), and cards whose upgrade does "X+1" add their own +1 on top of the resolved value, so all of them
/// stack. Hook.ModifyXValue runs in the synced card play, so every peer resolves the same X.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyXValue))]
internal static class PhantasmHauntedXValuePatch
{
    private static void Postfix(CardModel card, ref int __result)
    {
        try
        {
            if (card.EnergyCost.CostsX && card.TryGetModifier<PhantasmHauntedModifier>(out _))
            {
                __result += PhantasmalRules.HauntedXBonus;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Phantasm-Haunted X+1: {ex}");
        }
    }
}
