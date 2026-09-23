using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Patches;

/// <summary>A card bought from a Shadow Corruption shop joins the deck face up (ClearAfterPurchase only runs on a purchase).</summary>
[HarmonyPatch(typeof(MerchantCardEntry), "ClearAfterPurchase")]
internal static class ShadowShopCardPurchasePatch
{
    private static void Prefix(MerchantCardEntry __instance)
    {
        try
        {
            if (__instance.CreationResult?.Card is { } card)
            {
                ShadedCards.Unshade(card);
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow shop purchase", ex);
        }
    }
}

/// <summary>Shared look of hidden Shadow Corruption shop items: a shrouded icon and a hover tip that says nothing about the item.</summary>
internal static class ShroudedItem
{
    private static readonly string VanillaUnknownIconPath = ImageHelper.GetImagePath("atlases/ui_atlas.sprites/map/icons/map_unknown.tres");

    public static Texture2D? Icon => ModTextures.Get("shrouded_item.png", VanillaUnknownIconPath);

    /// <summary>Violet tint of hidden Shadow Corruption things (shop items, map nodes), multiplied over their own colours.</summary>
    public static readonly Color Tint = new(0.74f, 0.52f, 1f);

    /// <summary>Shows the shrouded look on a hidden item, and clears the tint otherwise (slots are reused after restocking).</summary>
    public static void Sync(bool hidden, TextureRect image, TextureRect? outline)
    {
        if (!hidden)
        {
            image.Modulate = Colors.White;
            return;
        }

        if (Icon is { } icon)
        {
            image.Texture = icon;
        }

        image.Modulate = Tint;

        // The outline follows the real item's silhouette, which would give it away.
        if (outline != null)
        {
            outline.Visible = false;
        }
    }

    public static void ShowTip(Control owner, string descriptionKey)
    {
        NHoverTipSet.Remove(owner);
        NHoverTipSet? tip = NHoverTipSet.CreateAndShow(
            owner,
            new HoverTip(ZoneTheSpireModifier.ModLoc("shrouded_title"), ZoneTheSpireModifier.ModLoc(descriptionKey)));
        tip?.SetGlobalPosition(owner.GlobalPosition);
    }
}

[HarmonyPatch(typeof(NMerchantRelic), "UpdateVisual")]
internal static class ShadowShopRelicVisualPatch
{
    private static readonly AccessTools.FieldRef<NMerchantRelic, NRelic?> RelicNode = SafeRef.Field<NMerchantRelic, NRelic?>("_relicNode");

    private static void Postfix(NMerchantRelic __instance)
    {
        try
        {
            if (RelicNode(__instance) is { } relicNode)
            {
                ShroudedItem.Sync(ShadowShop.IsHidden(__instance.Entry), relicNode.Icon, relicNode.Outline);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shroud a shop relic: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantRelic), "CreateHoverTip")]
internal static class ShadowShopRelicTipPatch
{
    private static bool Prefix(NMerchantRelic __instance)
    {
        try
        {
            if (!ShadowShop.IsHidden(__instance.Entry))
            {
                return true;
            }

            ShroudedItem.ShowTip(__instance, "shrouded_relic");
            return false;
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shrouded shop relic tip", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(NMerchantPotion), "UpdateVisual")]
internal static class ShadowShopPotionVisualPatch
{
    private static readonly AccessTools.FieldRef<NMerchantPotion, NPotion?> PotionNode = SafeRef.Field<NMerchantPotion, NPotion?>("_potionNode");

    private static void Postfix(NMerchantPotion __instance)
    {
        try
        {
            if (PotionNode(__instance) is { } potionNode)
            {
                ShroudedItem.Sync(ShadowShop.IsHidden(__instance.Entry), potionNode.Image, potionNode.Outline);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shroud a shop potion: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantPotion), "CreateHoverTip")]
internal static class ShadowShopPotionTipPatch
{
    private static bool Prefix(NMerchantPotion __instance)
    {
        try
        {
            if (!ShadowShop.IsHidden(__instance.Entry))
            {
                return true;
            }

            ShroudedItem.ShowTip(__instance, "shrouded_potion");
            return false;
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shrouded shop potion tip", ex);
            return true;
        }
    }
}
