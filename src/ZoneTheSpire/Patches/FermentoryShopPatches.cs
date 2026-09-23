using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// The Fermentory shops stock 6 potions instead of 3, from the player's own synced shop RNG like vanilla (so every peer stocks
/// the same). Outside the Fermentory, or on error, vanilla stocking runs.
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), "PopulatePotionEntries")]
internal static class FermentoryShopPotionsPatch
{
    private static readonly AccessTools.FieldRef<MerchantInventory, List<MerchantPotionEntry>> Entries =
        SafeRef.Field<MerchantInventory, List<MerchantPotionEntry>>("_potionEntries");

    private static bool Prefix(MerchantInventory __instance)
    {
        try
        {
            if (!BottleShop.IsBottleActive(__instance.Player))
            {
                return true;
            }

            List<PotionModel> potions = PotionFactory
                .CreateRandomPotionsOutOfCombat(__instance.Player, FermentoryRules.ShopPotionCount, __instance.Player.PlayerRng.Shops)
                .ToList();
            foreach (PotionModel potion in potions)
            {
                Entries(__instance).Add(new MerchantPotionEntry(potion.ToMutable(), __instance.Player));
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to stock the Fermentory shop's potions; stocking 3: {ex}");
            Entries(__instance).Clear();
            return true;
        }
    }
}

/// <summary>
/// The shop scene has one potion slot node per vanilla potion. When the inventory holds more (the Fermentory's 6), copies of
/// the first slot are added before the screen fills them. Presentation only.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class FermentoryShopPotionSlotsPatch
{
    private static void Prefix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        try
        {
            if (__instance.GetNodeOrNull<Control>("%Potions") is not { } container)
            {
                return;
            }

            List<NMerchantPotion> slots = container.GetChildren().OfType<NMerchantPotion>().ToList();
            if (slots.Count == 0)
            {
                return;
            }

            // A layout container places the copies itself; otherwise they form a second row under the vanilla slots.
            bool laidOut = container is Container;
            float rowHeight = Math.Max(slots[0].Size.Y, 120f) + 20f;
            for (int i = slots.Count; i < inventory.PotionEntries.Count; i++)
            {
                var copy = (Control)slots[0].Duplicate();
                if (!laidOut)
                {
                    copy.Position = slots[(i - slots.Count) % slots.Count].Position + new Vector2(0f, rowHeight * (1 + (i - slots.Count) / slots.Count));
                }

                container.AddChild(copy);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Fermentory shop potion slots: {ex}");
        }
    }
}
