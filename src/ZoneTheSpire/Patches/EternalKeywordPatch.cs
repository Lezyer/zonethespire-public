using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Patches;

/// <summary>
/// A card carrying the mod's Eternal modifier counts as Eternal everywhere the game reads a card's keywords: its text and tips
/// list Eternal, and card removal (IsRemovable) refuses it. The card's own keyword set is never touched; the patch hands back a
/// copy with the keyword added.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetKeywordsWithSources))]
internal static class EternalKeywordPatch
{
    private static void Postfix(CardModel __instance, ref IReadOnlySet<CardKeyword> __result)
    {
        try
        {
            if (EternalModifier.IsEternal(__instance))
            {
                __result = EternalModifier.WithEternal(__result);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to make a card Eternal: {ex.Message}");
        }
    }
}
