using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Deva's Domain shops (local presentation): the merchant screen has fixed slots (7 cards, 3 relics, 3 potions). Before it is
/// filled, the card slots are taken off the rug, enough relic and potion slots are added (instances of the vanilla slot scenes),
/// and every relic and potion slot is moved to its Deva's Domain place (DevasShopLayout). The rest of the vanilla filling,
/// navigation and purchase flow then runs unchanged.
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class DevasShopLayoutPatch
{
    private const string RelicScene = "res://scenes/merchant/merchant_relic.tscn";
    private const string PotionScene = "res://scenes/merchant/merchant_potion.tscn";

    private static readonly FieldInfo? CharacterCardsField = AccessTools.Field(typeof(NMerchantInventory), "_characterCardContainer");
    private static readonly FieldInfo? ColorlessCardsField = AccessTools.Field(typeof(NMerchantInventory), "_colorlessCardContainer");
    private static readonly FieldInfo? RelicsField = AccessTools.Field(typeof(NMerchantInventory), "_relicContainer");
    private static readonly FieldInfo? PotionsField = AccessTools.Field(typeof(NMerchantInventory), "_potionContainer");

    private static void Prefix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        try
        {
            if (!DevasShop.IsDevasShop(inventory)
                || RelicsField?.GetValue(__instance) is not Control relics
                || PotionsField?.GetValue(__instance) is not Control potions)
            {
                return;
            }

            RemoveCardSlots(CharacterCardsField?.GetValue(__instance) as Control);
            RemoveCardSlots(ColorlessCardsField?.GetValue(__instance) as Control);
            Layout<NMerchantRelic>(relics, RelicScene, inventory.RelicEntries.Count, DevasShopLayout.RelicSlot);
            Layout<NMerchantPotion>(potions, PotionScene, inventory.PotionEntries.Count, DevasShopLayout.PotionSlot);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to lay out a Deva's Domain shop: {ex}");
        }
    }

    private static void RemoveCardSlots(Control? container)
    {
        if (container == null)
        {
            return;
        }

        foreach (Node child in container.GetChildren().OfType<NMerchantCard>().ToList())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>
    /// Gives the container <paramref name="count"/> slots and places them. Slot positions are rug coordinates; the container sits
    /// on the rug, so each slot's position is its rug position minus the container's.
    /// </summary>
    private static void Layout<T>(Control container, string scenePath, int count, Func<int, (float X, float Y)> place)
        where T : Control
    {
        List<T> slots = container.GetChildren().OfType<T>().ToList();
        PackedScene? scene = slots.Count < count ? ResourceLoader.Load<PackedScene>(scenePath) : null;
        while (slots.Count < count && scene?.Instantiate() is T slot)
        {
            container.AddChild(slot);
            slots.Add(slot);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            (float x, float y) = place(i);
            Vector2 size = slots[i].Size;
            slots[i].Position = new Vector2(x, y) - container.Position;
            slots[i].Size = size;
        }
    }
}
