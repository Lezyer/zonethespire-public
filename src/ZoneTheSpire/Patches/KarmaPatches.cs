using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Run.Devas;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Karma: once a card has been paid for (manual plays; CardModel.SpendResources reads the cost before its first await, so the
/// postfix runs after the cost was taken), the discount its Karma actually gave is spent. Runs in the synced card play on every
/// peer.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class KarmaSpendPatch
{
    private static void Postfix(CardModel __instance) => Karma.AfterPaid(__instance);
}

/// <summary>A card with Karma or Chakra lists the Karma tip (after the card's own tips). Shaded cards show no tips at all.</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class KarmaHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        try
        {
            if (!ShadedCards.IsShaded(__instance) && Karma.NeedsTip(__instance))
            {
                __result = __result.Append(Karma.TipFor(__instance)).ToList();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Karma tip: {ex.Message}");
        }
    }
}
