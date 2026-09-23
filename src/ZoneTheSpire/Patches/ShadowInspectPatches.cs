using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Shadow Corruption hides things, and looking at them up close must not give them away. The inspect screen shows a fresh copy
/// of the card rather than the card itself, so the copy is shaded to match and its hover tips (built from the copy a moment
/// earlier) are cleared. Local presentation only.
/// </summary>
[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class ShadedCardInspectPatch
{
    private static readonly FieldInfo? CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");
    private static readonly FieldInfo? IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");
    private static readonly FieldInfo? CardField = AccessTools.Field(typeof(NInspectCardScreen), "_card");

    private static void Postfix(NInspectCardScreen __instance)
    {
        try
        {
            if (CardsField?.GetValue(__instance) is not IReadOnlyList<CardModel> cards
                || IndexField?.GetValue(__instance) is not int index
                || index < 0 || index >= cards.Count
                || !ShadedCards.IsShaded(cards[index]))
            {
                return;
            }

            if (CardField?.GetValue(__instance) is NCard node && node.Model is { } shown)
            {
                ShadedCards.Shade(shown);
                node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            }

            // The screen built its tips from the unshaded copy just before this ran; they would name the card's keywords.
            NHoverTipSet.Clear();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shade an inspected card: {ex.Message}");
        }
    }
}

/// <summary>A shrouded Shadow Corruption shop relic can't be inspected: looking at it up close would name it.</summary>
[HarmonyPatch(typeof(NMerchantRelic), "OnPreview")]
internal static class ShadowShopRelicPreviewPatch
{
    private static bool Prefix(NMerchantRelic __instance)
    {
        try
        {
            return !ShadowShop.IsHidden(__instance.Entry);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check a shrouded relic before inspecting it: {ex.Message}");
            return true;
        }
    }
}
