using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Patches;

/// <summary>
/// A Phantasm-Haunted card counts as Ethereal everywhere the game reads a card's keywords: the keyword tip shows wherever the
/// card is displayed, and at the end of the turn the game exhausts it while it is still in hand. The card's own keyword set is
/// never touched; the patch hands back a copy with the keyword added.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetKeywordsWithSources))]
internal static class PhantasmHauntedKeywordPatch
{
    private static void Postfix(CardModel __instance, ref IReadOnlySet<CardKeyword> __result)
    {
        try
        {
            if (PhantasmHauntedModifier.IsHaunted(__instance))
            {
                __result = PhantasmHauntedModifier.WithEthereal(__result);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to make a Phantasm-Haunted card Ethereal: {ex.Message}");
        }
    }
}
