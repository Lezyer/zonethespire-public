using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Rendering;

internal enum MirrorStrength
{
    Subtle,
    Glass,
}

/// <summary>Silvery, sheened "mirror" look for creature bodies. Local rendering only; never affects gameplay.</summary>
internal static class MirrorMaterial
{
    private const string ShaderCode = @"shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 0.35;
uniform float glass : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.78, 0.88, 1.0);
uniform float sheen_speed = 0.35;

void fragment() {
    vec4 base = COLOR;
    vec2 offset = vec2(TEXTURE_PIXEL_SIZE.x * 2.0 * glass, 0.0);
    float red = texture(TEXTURE, UV + offset).r;
    float blue = texture(TEXTURE, UV - offset).b;
    vec3 shifted = mix(base.rgb, vec3(red, base.g, blue), 0.5 * glass);
    float grey = dot(shifted, vec3(0.299, 0.587, 0.114));
    vec3 silver = mix(vec3(grey), vec3(grey) * tint * 1.15, 0.8);
    vec3 color = mix(shifted, silver, strength);
    float band = fract((UV.x + UV.y) * 0.5 - TIME * sheen_speed);
    float sheen = smoothstep(0.0, 0.08, band) * (1.0 - smoothstep(0.08, 0.18, band));
    color += vec3(sheen * 0.45 * strength);
    COLOR = vec4(color, base.a * (1.0 - 0.25 * glass));
}
";

    private static Shader? _shader;

    public static void Apply(Creature creature, MirrorStrength strength)
    {
        try
        {
            _shader ??= new Shader { Code = ShaderCode };
            var material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("strength", strength == MirrorStrength.Glass ? 0.75f : 0.35f);
            material.SetShaderParameter("glass", strength == MirrorStrength.Glass ? 1f : 0f);
            CreatureShaderMaterial.Apply(creature, material);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply mirror look: {ex}");
        }
    }

    public static void Remove(Creature creature) => CreatureShaderMaterial.Remove(creature);
}
