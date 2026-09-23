using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Blinding Hallows on the health bar: a gold segment grows from the end of the red HP toward the start (poison's geometry;
/// Doom keeps its own place at the start). It is a duplicate of the game's Doom segment recoloured to holy gold, with a gliding
/// band of light, a pale glow and rising light motes. Lethal Hallowed turns the whole remaining bar gold with a gold HP
/// label, the way Doom's lethal bar turns pink. Local rendering only; never affects gameplay.
/// </summary>
internal static class HallowedHealthBar
{
    private const string SegmentName = "ZoneTheSpireHallowedSegment";
    private const string MotesName = "ZoneTheSpireHallowedMotes";

    // Warm gold, with a bright band of light gliding along the bar, a pale glow along its top edge and fine twinkles.
    private const string SegmentShader = @"shader_type canvas_item;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    float band = exp(-pow((fract(UV.x * 0.5 - TIME * 0.45) - 0.5) * 7.0, 2.0));
    float top = (1.0 - smoothstep(0.0, 0.55, UV.y)) * 0.35;
    vec2 cell = floor(vec2(UV.x * 40.0, UV.y * 6.0));
    float h = hash(cell);
    float twinkle = step(0.9, h) * pow(max(0.0, sin(TIME * (2.0 + 3.0 * h) + h * 50.0)), 12.0);
    vec3 gold = vec3(0.96, 0.76, 0.28);
    vec3 light = vec3(1.0, 0.97, 0.82);
    vec3 col = mix(gold, light, clamp(band * 0.75 + top + twinkle, 0.0, 1.0));
    COLOR = vec4(col, base.a * COLOR.a);
}
";

    private static readonly FieldInfo? CreatureField = AccessTools.Field(typeof(NHealthBar), "_creature");
    private static readonly FieldInfo? DoomForegroundField = AccessTools.Field(typeof(NHealthBar), "_doomForeground");
    private static readonly FieldInfo? HpForegroundField = AccessTools.Field(typeof(NHealthBar), "_hpForeground");
    private static readonly FieldInfo? HpLabelField = AccessTools.Field(typeof(NHealthBar), "_hpLabel");
    private static readonly MethodInfo? FgWidthMethod = AccessTools.Method(typeof(NHealthBar), "GetFgWidth", new[] { typeof(int) });

    private static readonly Color LabelColor = new("FFEE9C");
    private static readonly Color LabelOutline = new("5A4500");

    private static readonly ConditionalWeakTable<Creature, List<WeakReference<NHealthBar>>> Bars = new();
    private static readonly ConditionalWeakTable<NHealthBar, StrongBox<bool>> Tinted = new();
    private static ShaderMaterial? _material;
    private static Texture2D? _mote;
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
            Log.WarnOnce("Hallowed health bar", ex);
        }
    }

    /// <summary>Hallowed and Doom changes don't refresh the bar on their own.</summary>
    public static void Refresh(Creature? creature)
    {
        try
        {
            if (creature == null || !Bars.TryGetValue(creature, out List<WeakReference<NHealthBar>>? bars))
            {
                return;
            }

            foreach (WeakReference<NHealthBar> reference in bars.ToArray())
            {
                if (reference.TryGetTarget(out NHealthBar? bar) && GodotObject.IsInstanceValid(bar))
                {
                    bar.RefreshValues();
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
                || DoomForegroundField?.GetValue(bar) is not Control doom || !GodotObject.IsInstanceValid(doom)
                || HpForegroundField?.GetValue(bar) is not Control hp || !GodotObject.IsInstanceValid(hp))
            {
                return;
            }

            int hallowed = creature.CurrentHp > 0 ? creature.GetPower<HallowedPower>()?.Amount ?? 0 : 0;
            Control? segment = doom.GetParent()?.GetNodeOrNull<Control>(SegmentName);
            bool lethal = hallowed > 0 && creature.CurrentHp <= hallowed;
            if (hallowed <= 0 || !hp.Visible)
            {
                // No Hallowed, or the game already took the whole bar (Doom or poison lethal): its own look wins.
                if (segment != null)
                {
                    segment.Visible = false;
                }

                Tint(bar, false);
                return;
            }

            segment ??= CreateSegment(doom, hp);
            if (segment == null)
            {
                return;
            }

            // Poison's recipe, anchored to where the red HP ends right now (the game has already carved out poison).
            float max = FgWidth(bar, creature.MaxHp);
            float end = max + hp.OffsetRight;
            float start = lethal ? 0f : Math.Max(0f, end - FgWidth(bar, hallowed));
            hp.OffsetRight = start - max;
            float marginLeft = segment is NinePatchRect patch ? patch.PatchMarginLeft : 0f;
            segment.OffsetLeft = Math.Max(0f, start - marginLeft);
            segment.OffsetRight = end - max;
            segment.Visible = true;
            if (segment.GetNodeOrNull<CpuParticles2D>(MotesName) is { } motes)
            {
                float width = Math.Max(1f, end - start);
                motes.Position = new Vector2(width / 2f + (start - segment.OffsetLeft), segment.Size.Y / 2f);
                motes.EmissionRectExtents = new Vector2(width / 2f, segment.Size.Y / 2f);
                motes.Amount = Math.Clamp((int)(width / 10f), 3, 24);
            }

            Tint(bar, lethal);
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    /// <summary>Gold label on a lethal bar; removes only the overrides this class added.</summary>
    private static void Tint(NHealthBar bar, bool lethal)
    {
        if (HpLabelField?.GetValue(bar) is not MegaLabel label)
        {
            return;
        }

        StrongBox<bool> tinted = Tinted.GetValue(bar, static _ => new StrongBox<bool>(false));
        if (lethal)
        {
            label.AddThemeColorOverride("font_color", LabelColor);
            label.AddThemeColorOverride("font_outline_color", LabelOutline);
            tinted.Value = true;
        }
        else if (tinted.Value)
        {
            label.RemoveThemeColorOverride("font_color");
            label.RemoveThemeColorOverride("font_outline_color");
            tinted.Value = false;
        }
    }

    private static float FgWidth(NHealthBar bar, int amount) =>
        FgWidthMethod?.Invoke(bar, new object[] { amount }) is float width ? width : 0f;

    private static Control? CreateSegment(Control doom, Control hp)
    {
        if (doom.GetParent() is not Node parent || doom.Duplicate() is not Control segment)
        {
            return null;
        }

        segment.Name = SegmentName;
        _material ??= new ShaderMaterial { Shader = new Shader { Code = SegmentShader } };
        var glow = new Color(1f, 0.93f, 0.7f);
        foreach (Node node in SelfAndDescendants(segment))
        {
            node.UniqueNameInOwner = false;
            if (node is Control control)
            {
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            switch (node)
            {
                // Doom's own effect particles, if its scene has any: the same motion, in holy light.
                case GpuParticles2D gpu:
                    gpu.Modulate = glow;
                    gpu.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
                    break;
                case CpuParticles2D cpu:
                    cpu.Color = glow;
                    cpu.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
                    break;
                case CanvasItem item:
                    item.Material = _material;
                    break;
            }
        }

        segment.AddChild(Motes());
        parent.AddChild(segment);
        // Directly above the red HP and Doom's segment (so a lethal gold bar covers both) but below anything drawn after them,
        // such as the HP label.
        int above = hp.GetParent() == parent ? Math.Max(doom.GetIndex(), hp.GetIndex()) : doom.GetIndex();
        parent.MoveChild(segment, above + 1);
        return segment;
    }

    /// <summary>Small light motes that rise off the gold and fade out, twinkling as they go.</summary>
    private static CpuParticles2D Motes() => new()
    {
        Name = MotesName,
        Amount = 10,
        Lifetime = 1.1,
        Preprocess = 1.1,
        LocalCoords = false,
        Texture = _mote ??= new GradientTexture2D
        {
            Width = 16,
            Height = 16,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) } },
        },
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(20f, 4f),
        Direction = new Vector2(0f, -1f),
        Spread = 25f,
        Gravity = new Vector2(0f, -18f),
        InitialVelocityMin = 6f,
        InitialVelocityMax = 16f,
        ScaleAmountMin = 0.25f,
        ScaleAmountMax = 0.6f,
        ColorRamp = new Gradient
        {
            Offsets = new[] { 0f, 0.2f, 0.6f, 1f },
            Colors = new[] { new Color(1f, 0.95f, 0.75f, 0f), new Color(1f, 0.95f, 0.75f, 0.9f), new Color(1f, 0.85f, 0.45f, 0.5f), new Color(1f, 0.85f, 0.45f, 0f) },
        },
        Emitting = true,
    };

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
            Log.Warn($"Failed to update a Hallowed health bar: {ex}");
        }
    }
}
