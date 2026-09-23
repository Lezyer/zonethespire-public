using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Forgotten Empire: Marbled on the health bar. A copy of the Block shield, recoloured to veined white marble, sits on the right
/// end of the bar (Block stays on the left) with the Marbled amount on it, and pops when Marbled grows. Any creature (enemy or
/// player) with Marbled and no Block (which keeps its blue bar) gets a white health bar with a dark grey text outline.
/// Local rendering only; never affects gameplay.
/// </summary>
internal static class MarbleHealthBar
{
    private const string ShieldName = "ZoneTheSpireMarbledShield";

    private const string ShieldShader = @"shader_type canvas_item;

void fragment() {
    vec4 base = COLOR;
    float luma = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec3 stone = mix(vec3(0.5, 0.5, 0.52), vec3(1.0, 0.99, 0.96), smoothstep(0.08, 0.62, luma));
    float wave = abs(sin(UV.x * 7.0 + UV.y * 3.0 + sin(UV.y * 9.0) * 0.8));
    float vein = 1.0 - smoothstep(0.0, 0.12, wave);
    stone = mix(stone, vec3(0.55, 0.55, 0.58), vein * 0.35 * step(0.3, luma));
    COLOR = vec4(stone, base.a);
}
";

    private static readonly FieldInfo? CreatureField = AccessTools.Field(typeof(NHealthBar), "_creature");
    private static readonly FieldInfo? BlockContainerField = AccessTools.Field(typeof(NHealthBar), "_blockContainer");
    private static readonly FieldInfo? BlockLabelField = AccessTools.Field(typeof(NHealthBar), "_blockLabel");
    private static readonly FieldInfo? ForegroundField = AccessTools.Field(typeof(NHealthBar), "_hpForeground");
    private static readonly FieldInfo? HpLabelField = AccessTools.Field(typeof(NHealthBar), "_hpLabel");
    private static readonly FieldInfo? OriginalBlockPositionField = AccessTools.Field(typeof(NHealthBar), "_originalBlockPosition");

    private static readonly Color RedForeground = new("F1373E");
    private static readonly Color DefaultOutline = new("900000");
    private static readonly Color MarbleForeground = new(0.94f, 0.93f, 0.9f);
    private static readonly Color MarbleOutline = new("3A3A3E");

    private static readonly ConditionalWeakTable<Creature, List<WeakReference<NHealthBar>>> Bars = new();
    private static ShaderMaterial? _shieldMaterial;
    private static bool _warned;

    public static void Register(Creature creature, NHealthBar bar)
    {
        try
        {
            List<WeakReference<NHealthBar>> bars = Bars.GetOrCreateValue(creature);
            bars.RemoveAll(reference => !reference.TryGetTarget(out NHealthBar? existing) || !GodotObject.IsInstanceValid(existing));
            bars.Add(new WeakReference<NHealthBar>(bar));
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Marbled health bar", ex);
        }
    }

    /// <summary>Refreshes every health bar showing this creature (Marbled changes don't trigger the game's own refresh).</summary>
    public static void Refresh(Creature creature, bool pulse)
    {
        try
        {
            if (!Bars.TryGetValue(creature, out List<WeakReference<NHealthBar>>? bars))
            {
                return;
            }

            foreach (WeakReference<NHealthBar> reference in bars.ToArray())
            {
                if (!reference.TryGetTarget(out NHealthBar? bar) || !GodotObject.IsInstanceValid(bar))
                {
                    continue;
                }

                bar.RefreshValues();
                if (pulse && ShieldOf(bar) is { Visible: true } shield)
                {
                    shield.Scale = new Vector2(1.35f, 1.35f);
                    shield.CreateTween().TweenProperty(shield, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                }
            }
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    public static void OnRefreshed(NHealthBar bar)
    {
        try
        {
            if (CreatureField?.GetValue(bar) is not Creature creature
                || BlockContainerField?.GetValue(bar) is not Control block
                || !GodotObject.IsInstanceValid(block))
            {
                return;
            }

            int marbled = creature.CurrentHp > 0 ? creature.GetPower<MarbledPower>()?.Amount ?? 0 : 0;
            Control? shield = ShieldOf(bar);
            if (marbled <= 0)
            {
                if (shield != null)
                {
                    shield.Visible = false;
                }

                return;
            }

            shield ??= CreateShield(block);
            if (shield == null)
            {
                return;
            }

            shield.Visible = true;
            shield.Modulate = Colors.White;
            if (BlockLabelField?.GetValue(bar) is Node blockLabel && shield.GetNodeOrNull<MegaLabel>(block.GetPathTo(blockLabel)) is { } label)
            {
                label.SetTextAutoSize(marbled.ToString());
                label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, MarbleOutline);
            }

            Place(bar, block, shield);
            Whiten(bar);
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    private static Control? ShieldOf(NHealthBar bar) =>
        BlockContainerField?.GetValue(bar) is Control block && GodotObject.IsInstanceValid(block)
            ? block.GetParent()?.GetNodeOrNull<Control>(ShieldName)
            : null;

    private static Control? CreateShield(Control block)
    {
        if (block.GetParent() is not Node parent || block.Duplicate() is not Control shield)
        {
            return null;
        }

        shield.Name = ShieldName;
        foreach (Node node in SelfAndDescendants(shield))
        {
            node.UniqueNameInOwner = false;
            if (node is Control control)
            {
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            if (node != shield && node is CanvasItem item && node is not Label)
            {
                item.Material = _shieldMaterial ??= new ShaderMaterial { Shader = new Shader { Code = ShieldShader } };
            }
        }

        parent.AddChild(shield);
        parent.MoveChild(shield, block.GetIndex() + 1);
        return shield;
    }

    /// <summary>Mirrors the Block shield's spot on the left end of the bar to the right end.</summary>
    private static void Place(NHealthBar bar, Control block, Control shield)
    {
        if (block.GetParent() is not CanvasItem parent || bar.HpBarContainer is not { } container)
        {
            return;
        }

        Vector2 original = OriginalBlockPositionField?.GetValue(bar) is Vector2 position ? position : block.Position;
        Transform2D transform = parent.GetGlobalTransform();
        float scale = Math.Max(0.0001f, transform.Scale.X);
        float blockCenter = (transform * original).X + block.Size.X * 0.5f * scale;
        float barLeft = container.GlobalPosition.X;
        float barRight = barLeft + container.Size.X * container.GetGlobalTransform().Scale.X;
        float shieldCenter = barRight - (blockCenter - barLeft);
        shield.Position = new Vector2(original.X + (shieldCenter - blockCenter) / scale, original.Y);
        shield.PivotOffset = shield.Size * 0.5f;
    }

    private static void Whiten(NHealthBar bar)
    {
        if (ForegroundField?.GetValue(bar) is Control foreground && foreground.SelfModulate.IsEqualApprox(RedForeground))
        {
            foreground.SelfModulate = MarbleForeground;
        }

        if (HpLabelField?.GetValue(bar) is Label hpLabel
            && hpLabel.GetThemeColor(ThemeConstants.Label.FontOutlineColor).IsEqualApprox(DefaultOutline))
        {
            hpLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, MarbleOutline);
        }
    }

    private static IEnumerable<Node> SelfAndDescendants(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node node in SelfAndDescendants(child))
            {
                yield return node;
            }
        }
    }

    private static void Warn(Exception ex)
    {
        if (!_warned)
        {
            _warned = true;
            Log.Warn($"Failed to update a Marbled health bar: {ex}");
        }
    }
}
