using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Zone changes applied as soon as a normal merchant's inventory is created (every peer): Phantasmal Tombs empties raided
/// slots, Deva's Domain swaps the cards for more relics and potions, Ferrosand makes 3 cards Magnetic, Shadow Corruption
/// shades 4 cards and hides relics and potions. Each checks its own
/// zone effect.
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
internal static class RaidedShopInventoryPatch
{
    private static void Postfix(MerchantInventory __result)
    {
        try
        {
            RaidedShop.Raid(__result);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to raid a Phantasmal Tombs shop: {ex}");
        }

        try
        {
            DevasShop.Restock(__result);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to restock a Deva's Domain shop: {ex}");
        }

        try
        {
            FerrosandShop.Magnetize(__result);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to magnetize Ferrosand shop cards: {ex}");
        }

        try
        {
            ShadowShop.Shroud(__result);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shroud a Shadow Corruption shop: {ex}");
        }
    }
}

/// <summary>
/// Local presentation only: after the inventory has populated, tint the rug's own TextureRect with moving ectoplasm.
/// Merchandise is rendered by child controls and is not processed by the rug material.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class PhantasmalMerchantRugPatch
{
    private static void Postfix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        MerchantRugEctoplasm.Apply(__instance, inventory);
    }
}
