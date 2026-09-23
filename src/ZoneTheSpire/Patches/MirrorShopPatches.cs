using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// The removal flow runs for the buyer locally and for every other peer through MerchantCardRemovalMessage, so replacing it
/// here keeps zone shop services (Mirrorlands Duplicate, Scrapyard Scrap, Prismatic Storm Transform, Fermentory Bottle) in sync. Outside those zones (or on error)
/// vanilla removal runs.
/// </summary>
[HarmonyPatch(typeof(OneOffSynchronizer), "DoMerchantCardRemoval")]
internal static class MerchantServiceFlowPatch
{
    private static bool Prefix(Player player, int goldCost, bool cancelable, ref Task<bool> __result)
    {
        try
        {
            ShopService? service = ShopServices.For(player);
            if (service == ShopServices.Scrap)
            {
                __result = ScrapShop.ScrapFromDeck(player, cancelable);
                return false;
            }

            if (service == ShopServices.Duplicate)
            {
                __result = MirrorShop.DuplicateFromDeck(player, goldCost, cancelable);
                return false;
            }

            if (service == ShopServices.Transform)
            {
                __result = TransformShop.TransformFromDeck(player, goldCost, cancelable);
                return false;
            }

            if (service == ShopServices.Bottle)
            {
                __result = BottleShop.Buy(player, goldCost);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Zone shop service failed to start; using card removal: {ex}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(NMerchantCardRemoval), "Title", MethodType.Getter)]
internal static class MerchantServiceTitlePatch
{
    private static void Postfix(NMerchantCardRemoval __instance, ref LocString __result)
    {
        try
        {
            if (ShopServices.ForSlot(__instance) is { } service)
            {
                __result = ZoneTheSpireModifier.ModLoc(service.TitleKey);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply a zone shop service title: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantCardRemoval), "Description", MethodType.Getter)]
internal static class MerchantServiceDescriptionPatch
{
    private static void Postfix(NMerchantCardRemoval __instance, ref LocString __result)
    {
        try
        {
            if (ShopServices.ForSlot(__instance) is { } service)
            {
                __result = ZoneTheSpireModifier.ModLoc(service.DescriptionKey);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply a zone shop service description: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantCardRemoval), nameof(NMerchantCardRemoval.FillSlot))]
internal static class MerchantServiceVisualPatch
{
    private static void Postfix(NMerchantCardRemoval __instance)
    {
        try
        {
            if (ShopServices.ForSlot(__instance) is { } service)
            {
                ShopServices.ApplyVisual(__instance, service);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply a zone shop service visual: {ex}");
        }
    }
}
