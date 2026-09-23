using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Fermentory;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// The Fermentory's special potion slots in the top bar: a ring tinted per effect on each special slot, the slot's effect added
/// to its hover tip (also when empty), and a scrolling window when there are more than FermentoryRules.VisibleSlots slots (the
/// rest are hidden; the mouse wheel and controller focus scroll, and arrows show that more slots exist). Local rendering only.
/// </summary>
internal static class SpecialSlotVisuals
{
    private const string RingName = "ZoneTheSpireSpecialRing";
    private const string MoreLeftName = "ZoneTheSpireMoreLeft";
    private const string MoreRightName = "ZoneTheSpireMoreRight";
    private const string WheelMeta = "zts_wheel";

    private static readonly AccessTools.FieldRef<NPotionContainer, Player?> PlayerRef =
        SafeRef.Field<NPotionContainer, Player?>("_player");

    private static readonly AccessTools.FieldRef<NPotionContainer, List<NPotionHolder>> HoldersRef =
        SafeRef.Field<NPotionContainer, List<NPotionHolder>>("_holders");

    private static readonly Dictionary<ulong, int> Offsets = new();
    private static Texture2D? _ring;
    private static bool _subscribed;

    /// <summary>Between runs (RunLifecycle): the potion row starts scrolled to the left.</summary>
    internal static void Reset() => Offsets.Clear();

    public static void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        SpecialSlots.Changed += player =>
        {
            if (LocalContext.IsMe(player) && NRun.Instance?.GlobalUi.TopBar.PotionContainer is { } container)
            {
                Callable.From(() => Refresh(container)).CallDeferred();
            }
        };
    }

    public static Color Tint(SlotEffect effect) => effect switch
    {
        SlotEffect.DuplicatingSolution => new Color(0.35f, 0.9f, 1f),
        SlotEffect.EntropicSolvent => new Color(0.7f, 0.4f, 1f),
        SlotEffect.BottomlessMechanism => new Color(0.25f, 0.45f, 1f),
        SlotEffect.DistributionSystem => new Color(1f, 0.6f, 0.2f),
        SlotEffect.HealingBalm => new Color(0.35f, 1f, 0.45f),
        SlotEffect.EmpoweringDraught => new Color(1f, 0.25f, 0.2f),
        SlotEffect.WardingFlask => new Color(0.7f, 0.8f, 0.9f),
        SlotEffect.QuickeningTonic => new Color(1f, 0.95f, 0.3f),
        SlotEffect.VolatileMixture => new Color(1f, 0.35f, 0.75f),
        _ => new Color(1f, 0.78f, 0.35f),
    };

    public static void Refresh(NPotionContainer container)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(container) || PlayerRef(container) is not { } player)
            {
                return;
            }

            List<NPotionHolder> holders = HoldersRef(container);
            IReadOnlyDictionary<int, SlotEffect> special = SpecialSlots.Get(player);
            for (int slot = 0; slot < holders.Count; slot++)
            {
                NPotionHolder holder = holders[slot];
                SetRing(holder, special.TryGetValue(slot, out SlotEffect effect) ? effect : null);
                if (!holder.HasMeta(WheelMeta))
                {
                    holder.SetMeta(WheelMeta, true);
                    holder.GuiInput += input => OnWheel(container, input);
                    holder.FocusEntered += () => ScrollTo(container, holders.IndexOf(holder));
                }
            }

            ApplyWindow(container, player, holders);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh special potion slots: {ex}");
        }
    }

    /// <summary>Scrolls so the slot is visible with a neighbour on each side when there is one (so controller focus can move on).</summary>
    public static void ScrollTo(NPotionContainer container, int slot)
    {
        try
        {
            if (PlayerRef(container) is not { } player || slot < 0)
            {
                return;
            }

            List<NPotionHolder> holders = HoldersRef(container);
            int visible = FermentoryRules.VisibleSlots;
            if (holders.Count <= visible)
            {
                return;
            }

            int offset = Offsets.GetValueOrDefault(player.NetId);
            if (slot <= offset)
            {
                offset = slot - 1;
            }
            else if (slot >= offset + visible - 1)
            {
                offset = slot - visible + 2;
            }

            Offsets[player.NetId] = offset;
            ApplyWindow(container, player, holders);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to scroll the potion slots: {ex}");
        }
    }

    /// <summary>The effect's tip for a hovered special slot, or null.</summary>
    public static IHoverTip? TipFor(NPotionHolder holder)
    {
        if (holder.GetParent()?.GetParent()?.GetParent() is not NPotionContainer container || PlayerRef(container) is not { } player)
        {
            return null;
        }

        int slot = HoldersRef(container).IndexOf(holder);
        return SpecialSlots.EffectAt(player, slot) is SlotEffect effect
            ? new HoverTip(ZoneTheSpireModifier.ModTextLoc(FermentoryText.NameKey(effect)), FermentoryText.Description(effect))
            : null;
    }

    private static void OnWheel(NPotionContainer container, InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } button
            || (button.ButtonIndex != MouseButton.WheelUp && button.ButtonIndex != MouseButton.WheelDown)
            || PlayerRef(container) is not { } player)
        {
            return;
        }

        int step = button.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
        Offsets[player.NetId] = Offsets.GetValueOrDefault(player.NetId) + step;
        ApplyWindow(container, player, HoldersRef(container));
    }

    private static void ApplyWindow(NPotionContainer container, Player player, List<NPotionHolder> holders)
    {
        int visible = FermentoryRules.VisibleSlots;
        int offset = Math.Clamp(Offsets.GetValueOrDefault(player.NetId), 0, Math.Max(0, holders.Count - visible));
        Offsets[player.NetId] = offset;
        for (int slot = 0; slot < holders.Count; slot++)
        {
            NPotionHolder holder = holders[slot];
            holder.Visible = holders.Count <= visible || (slot >= offset && slot < offset + visible);
            SetArrow(holder, MoreLeftName, "‹", holders.Count > visible && slot == offset && offset > 0, left: true);
            SetArrow(holder, MoreRightName, "›", holders.Count > visible && slot == offset + visible - 1 && slot < holders.Count - 1, left: false);
        }
    }

    private static void SetRing(NPotionHolder holder, SlotEffect? effect)
    {
        TextureRect? ring = holder.GetNodeOrNull<TextureRect>(RingName);
        if (effect is not SlotEffect slotEffect)
        {
            ring?.QueueFree();
            return;
        }

        if (ring == null)
        {
            ring = new TextureRect
            {
                Name = RingName,
                Texture = Ring(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            ring.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            ring.OffsetLeft = -6f;
            ring.OffsetTop = -6f;
            ring.OffsetRight = 6f;
            ring.OffsetBottom = 6f;
            holder.AddChild(ring);
            holder.MoveChild(ring, 0);
            Tween pulse = ring.CreateTween().SetLoops();
            pulse.TweenProperty(ring, "modulate:a", 0.55f, 1.3).SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(ring, "modulate:a", 1f, 1.3).SetTrans(Tween.TransitionType.Sine);
        }

        ring.SelfModulate = Tint(slotEffect);
    }

    private static void SetArrow(NPotionHolder holder, string name, string glyph, bool show, bool left)
    {
        Label? arrow = holder.GetNodeOrNull<Label>(name);
        if (!show)
        {
            arrow?.QueueFree();
            return;
        }

        if (arrow != null)
        {
            return;
        }

        arrow = new Label
        {
            Name = name,
            Text = glyph,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 0.9f, 0.6f),
        };
        arrow.AddThemeFontSizeOverride("font_size", 34);
        arrow.AddThemeColorOverride("font_outline_color", Colors.Black);
        arrow.AddThemeConstantOverride("outline_size", 8);
        arrow.Position = new Vector2(left ? -18f : holder.Size.X + 2f, holder.Size.Y / 2f - 24f);
        holder.AddChild(arrow);
    }

    /// <summary>A soft ring (drawn once into an image), tinted per effect.</summary>
    private static Texture2D Ring()
    {
        if (_ring != null)
        {
            return _ring;
        }

        const int size = 96;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).Length() / (size / 2f);
                float ring = Mathf.Clamp(1f - Math.Abs(d - 0.82f) / 0.14f, 0f, 1f);
                float glow = Mathf.Clamp(1f - d, 0f, 1f) * 0.25f;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, Math.Min(1f, ring * ring + glow)));
            }
        }

        return _ring = ImageTexture.CreateFromImage(image);
    }
}

[HarmonyPatch(typeof(NPotionContainer), nameof(NPotionContainer.Initialize))]
internal static class SpecialSlotInitializePatch
{
    private static void Postfix(NPotionContainer __instance)
    {
        try
        {
            SpecialSlotVisuals.Subscribe();
            SpecialSlotVisuals.Refresh(__instance);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Special potion slots (potion row)", ex);
        }
    }
}

[HarmonyPatch(typeof(NPotionContainer), "GrowPotionHolders")]
internal static class SpecialSlotGrowPatch
{
    private static void Postfix(NPotionContainer __instance) => SpecialSlotVisuals.Refresh(__instance);
}

/// <summary>A potion arriving in a hidden slot scrolls the row to it first, so its fly-in animation lands on screen.</summary>
[HarmonyPatch(typeof(NPotionContainer), nameof(NPotionContainer.AnimatePotion))]
internal static class SpecialSlotAnimatePatch
{
    private static void Prefix(NPotionContainer __instance, PotionModel potion)
    {
        try
        {
            SpecialSlotVisuals.ScrollTo(__instance, potion.Owner.GetPotionSlotIndex(potion));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to scroll to a new potion: {ex}");
        }
    }
}

/// <summary>A special slot's hover tip also names its effect (after the potion's own tips, or alone for an empty slot).</summary>
[HarmonyPatch(typeof(NPotionHolder), "OnFocus")]
internal static class SpecialSlotTipPatch
{
    private static void Postfix(NPotionHolder __instance)
    {
        try
        {
            if (SpecialSlotVisuals.TipFor(__instance) is not { } slotTip)
            {
                return;
            }

            var tips = new List<IHoverTip>();
            if (__instance.Potion?.Model is { } potion)
            {
                tips.AddRange(potion.HoverTips);
            }

            tips.Add(slotTip);
            NHoverTipSet.Remove(__instance);
            NHoverTipSet? set = NHoverTipSet.CreateAndShow(__instance, tips, HoverTipAlignment.Center);
            set?.SetGlobalPosition(__instance.GlobalPosition + Vector2.Down * __instance.Size.Y * Mathf.Max(1.5f, __instance.Scale.Y));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show a special slot's tip: {ex}");
        }
    }
}
