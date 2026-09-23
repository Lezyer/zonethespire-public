using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Blood Rain shops (and prices a Blood Ledger holder can't afford in gold) show prices in HP: after each slot writes its gold
/// price (NMerchantSlot.UpdateVisual and every override), the price label is rewritten with the HP cost and the gold coin next
/// to it becomes the Blood Drinker icon (restored when the price is paid in gold again).
/// Visual only; the price colour already follows HP through BloodShopAffordabilityPatch.
/// </summary>
[HarmonyPatch]
internal static class BloodShopPriceDisplayPatch
{
    private const string SwappedMeta = "zonethespire_blood_price_icon";
    private const string OriginalTextureMeta = "zonethespire_blood_price_original_texture";
    private const string OriginalExpandMeta = "zonethespire_blood_price_original_expand";
    private const string OriginalStretchMeta = "zonethespire_blood_price_original_stretch";
    private const string OriginalScaleMeta = "zonethespire_blood_price_original_scale";

    private static readonly FieldInfo? CostLabelField = AccessTools.Field(typeof(NMerchantSlot), "_costLabel");
    private static readonly MethodInfo? EntryGetter = AccessTools.PropertyGetter(typeof(NMerchantSlot), "Entry");
    private static readonly string VanillaBurstIconPath = ImageHelper.GetImagePath("powers/burst_power.png");
    private static bool _loggedIcon;

    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(NMerchantSlot).Assembly.GetTypes()
            .Where(type => type == typeof(NMerchantSlot) || type.IsSubclassOf(typeof(NMerchantSlot)))
            .Select(type => AccessTools.DeclaredMethod(type, "UpdateVisual"))
            .Where(method => method != null && !method.IsAbstract)
            .Cast<MethodBase>();

    private static void Postfix(NMerchantSlot __instance)
    {
        try
        {
            if (EntryGetter?.Invoke(__instance, null) is not MerchantEntry entry
                || !entry.IsStocked
                || CostLabelField?.GetValue(__instance) is not MegaLabel label)
            {
                return;
            }

            // With the Blood Ledger a price can switch between gold and HP during a visit, so the coin comes back too.
            if (!BloodShop.PaysInHp(entry))
            {
                RestoreCoinIcon(label);
                return;
            }

            label.SetTextAutoSize(BloodShop.HpCostOf(entry).ToString());
            SwapCoinIcon(label);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show a Blood Rain shop price in HP: {ex}");
        }
    }

    /// <summary>Replaces the gold coin drawn beside the price with the Blood Drinker icon, at the coin's on-screen size.</summary>
    private static void SwapCoinIcon(Control label)
    {
        Node? container = label.GetParent();
        if (container == null || container.HasMeta(SwappedMeta))
        {
            return;
        }

        Texture2D? blood = ModTextures.Get("blood_drinker.png", VanillaBurstIconPath);
        if (blood == null)
        {
            return;
        }

        var swapped = new List<string>();
        foreach (Node node in Descendants(container, 3))
        {
            if (node == label || !LooksLikeGold(node))
            {
                continue;
            }

            switch (node)
            {
                case TextureRect rect:
                    rect.SetMeta(OriginalTextureMeta, rect.Texture);
                    rect.SetMeta(OriginalExpandMeta, (int)rect.ExpandMode);
                    rect.SetMeta(OriginalStretchMeta, (int)rect.StretchMode);
                    rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    rect.Texture = blood;
                    swapped.Add(rect.Name);
                    break;
                case Sprite2D sprite when sprite.Texture != null:
                    sprite.SetMeta(OriginalTextureMeta, sprite.Texture);
                    sprite.SetMeta(OriginalScaleMeta, sprite.Scale);
                    Vector2 shown = sprite.Texture.GetSize() * sprite.Scale;
                    sprite.Texture = blood;
                    sprite.Scale = shown / blood.GetSize();
                    swapped.Add(sprite.Name);
                    break;
            }
        }

        container.SetMeta(SwappedMeta, true);
        if (swapped.Count == 0 && !_loggedIcon)
        {
            _loggedIcon = true;
            Log.Warn($"Blood Rain shop: no gold icon found beside the price; nodes: {string.Join(", ", Descendants(container, 3).Select(n => $"{n.Name}:{n.GetType().Name}"))}.");
        }
    }

    /// <summary>Puts the gold coin back beside a price that is paid in gold again (only undoes a swap this patch made).</summary>
    private static void RestoreCoinIcon(Control label)
    {
        Node? container = label.GetParent();
        if (container == null || !container.HasMeta(SwappedMeta))
        {
            return;
        }

        foreach (Node node in Descendants(container, 3))
        {
            if (!node.HasMeta(OriginalTextureMeta))
            {
                continue;
            }

            switch (node)
            {
                case TextureRect rect:
                    rect.Texture = rect.GetMeta(OriginalTextureMeta).As<Texture2D>();
                    rect.ExpandMode = (TextureRect.ExpandModeEnum)rect.GetMeta(OriginalExpandMeta).AsInt32();
                    rect.StretchMode = (TextureRect.StretchModeEnum)rect.GetMeta(OriginalStretchMeta).AsInt32();
                    break;
                case Sprite2D sprite:
                    sprite.Texture = sprite.GetMeta(OriginalTextureMeta).As<Texture2D>();
                    sprite.Scale = sprite.GetMeta(OriginalScaleMeta).AsVector2();
                    break;
            }

            node.RemoveMeta(OriginalTextureMeta);
        }

        container.RemoveMeta(SwappedMeta);
    }

    private static bool LooksLikeGold(Node node)
    {
        string path = node switch
        {
            TextureRect rect => rect.Texture?.ResourcePath ?? string.Empty,
            Sprite2D sprite => sprite.Texture?.ResourcePath ?? string.Empty,
            _ => string.Empty,
        };
        return (node is TextureRect or Sprite2D)
               && (path.Contains("gold", StringComparison.OrdinalIgnoreCase)
                   || node.Name.ToString().Contains("gold", StringComparison.OrdinalIgnoreCase)
                   || node.Name.ToString().Contains("coin", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Node> Descendants(Node root, int depth)
    {
        if (depth <= 0)
        {
            yield break;
        }

        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node nested in Descendants(child, depth - 1))
            {
                yield return nested;
            }
        }
    }
}
