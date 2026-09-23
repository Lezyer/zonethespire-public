using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Effects;

/// <summary>A zone service that takes over the shop's card removal slot (text keys and replacement texture prefix).</summary>
internal sealed record ShopService(string Name, string TitleKey, string DescriptionKey, string TexturePrefix);

/// <summary>Which zone service (if any) replaces the card removal slot, and its slot presentation.</summary>
internal static class ShopServices
{
    public static readonly ShopService Duplicate = new("Duplicate", "duplicate_title", "duplicate_description", "duplicate");
    public static readonly ShopService Scrap = new("Scrap", "scrap_title", "scrap_description", "scrap");
    public static readonly ShopService Transform = new("Transform", "transform_title", "transform_description", "transform");
    public static readonly ShopService Bottle = new("Bottle", "bottle_title", "bottle_description", "bottle");

    private static readonly FieldInfo? EntryPlayerField = AccessTools.Field(typeof(MerchantEntry), "_player");
    private static readonly Regex RemovalFrame = new(@"card_removal_(\d\d)\.png$", RegexOptions.CultureInvariant);
    private static readonly string[] FrameNames = { "00", "01", "02", "04", "05" };

    public static ShopService? For(Player player) =>
        ScrapShop.IsScrapActive(player) ? Scrap
        : MirrorShop.IsDuplicateActive(player) ? Duplicate
        : TransformShop.IsTransformActive(player) ? Transform
        : BottleShop.IsBottleActive(player) ? Bottle
        : null;

    public static ShopService? ForSlot(NMerchantCardRemoval slot)
    {
        try
        {
            return slot.Entry is MerchantEntry entry && EntryPlayerField?.GetValue(entry) is Player player ? For(player) : null;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check the shop service slot: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Swaps the slot's removal frames for textures/&lt;prefix&gt;_NN.png on the Visual sprite, its Visual2 shadow and every
    /// "Visual:texture" animation key (merchant_card_removal.tscn). The animation library is duplicated first so the shared
    /// vanilla resource (used by other shops) is never modified. Warns when no removal frames are recognised or the swap fails.
    /// </summary>
    public static void ApplyVisual(NMerchantCardRemoval slot, ShopService service)
    {
        try
        {
            Sprite2D? sprite = slot.GetNodeOrNull<Sprite2D>("%Visual");
            Sprite2D? shadow = slot.GetNodeOrNull<Sprite2D>("%Visual2");
            AnimationPlayer? animator = slot.GetNodeOrNull<AnimationPlayer>("%Animation");
            Dictionary<Texture2D, Texture2D> replacements = BuildReplacements(sprite, animator, service.TexturePrefix);
            if (replacements.Count == 0)
            {
                Log.Warn($"{service.Name} shop visual: no removal frames recognised (visual texture '{sprite?.Texture?.ResourcePath}', animator {(animator == null ? "missing" : "found")}).");
                return;
            }

            foreach (Sprite2D? node in new[] { sprite, shadow })
            {
                if (node?.Texture != null && replacements.TryGetValue(node.Texture, out Texture2D? replacement))
                {
                    node.Texture = replacement;
                }
            }

            if (animator != null)
            {
                string playing = animator.CurrentAnimation;
                double position = string.IsNullOrEmpty(playing) ? 0 : animator.CurrentAnimationPosition;
                foreach (StringName libraryName in animator.GetAnimationLibraryList())
                {
                    var library = (AnimationLibrary)animator.GetAnimationLibrary(libraryName).Duplicate(true);
                    bool changed = false;
                    foreach (StringName animationName in library.GetAnimationList())
                    {
                        Animation animation = library.GetAnimation(animationName);
                        for (int track = 0; track < animation.GetTrackCount(); track++)
                        {
                            for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
                            {
                                if (animation.TrackGetKeyValue(track, key).Obj is Texture2D texture
                                    && replacements.TryGetValue(texture, out Texture2D? replacement))
                                {
                                    animation.TrackSetKeyValue(track, key, replacement);
                                    changed = true;
                                }
                            }
                        }
                    }

                    if (changed)
                    {
                        animator.RemoveAnimationLibrary(libraryName);
                        animator.AddAnimationLibrary(libraryName, library);
                    }
                }

                if (!string.IsNullOrEmpty(playing))
                {
                    animator.Play(playing);
                    animator.Seek(position, true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply the {service.Name} shop visual: {ex}");
        }
    }

    /// <summary>
    /// Maps each vanilla removal frame texture to its replacement. A frame is recognised by its resource path
    /// (card_removal_NN.png) when available, otherwise by its order among the distinct textures of the "Used" animation,
    /// which vanilla keys as 00, 00, 01, 02, 04, 05.
    /// </summary>
    private static Dictionary<Texture2D, Texture2D> BuildReplacements(Sprite2D? sprite, AnimationPlayer? animator, string prefix)
    {
        var map = new Dictionary<Texture2D, Texture2D>(ReferenceEqualityComparer.Instance);
        var ordered = new List<Texture2D>();
        if (animator != null && animator.HasAnimation("Used"))
        {
            Animation used = animator.GetAnimation("Used");
            for (int track = 0; track < used.GetTrackCount(); track++)
            {
                for (int key = 0; key < used.TrackGetKeyCount(track); key++)
                {
                    if (used.TrackGetKeyValue(track, key).Obj is Texture2D texture && !ordered.Contains(texture))
                    {
                        ordered.Add(texture);
                    }
                }
            }
        }

        if (sprite?.Texture is { } visual && !ordered.Contains(visual))
        {
            ordered.Insert(0, visual);
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            Texture2D vanilla = ordered[i];
            string path = vanilla.ResourcePath ?? string.Empty;
            Match match = RemovalFrame.Match(path);
            string? frame = match.Success ? match.Groups[1].Value
                : ordered.Count == FrameNames.Length ? FrameNames[i]
                : null;
            if (frame == null)
            {
                continue;
            }

            Texture2D? custom = ModTextures.Get($"{prefix}_{frame}.png", path);
            if (custom != null && !ReferenceEquals(custom, vanilla))
            {
                map[vanilla] = custom;
            }
        }

        return map;
    }
}
