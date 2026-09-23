using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Rainbow "prism" look for Prismatic Storm enemies: a slowly drifting hue tint plus a bright rainbow sheen band.
/// Local rendering only; never affects gameplay.
/// </summary>
internal static class PrismMaterial
{
    private const string ShaderCode = @"shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 0.3;
uniform float drift_speed = 0.12;
uniform float sheen_speed = 0.3;

vec3 hue_to_rgb(float hue) {
    return clamp(abs(mod(hue * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}

void fragment() {
    vec4 base = COLOR;
    float luma = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec3 rainbow = hue_to_rgb(fract((UV.x * 0.7 + UV.y * 0.5) - TIME * drift_speed));
    vec3 color = mix(base.rgb, base.rgb * 0.65 + rainbow * (0.35 + 0.5 * luma), strength);
    float band = fract((UV.x + UV.y) * 0.5 - TIME * sheen_speed);
    float sheen = smoothstep(0.0, 0.07, band) * (1.0 - smoothstep(0.07, 0.16, band));
    color += hue_to_rgb(fract(UV.y - TIME * drift_speed * 2.0)) * sheen * 0.4;
    COLOR = vec4(color, base.a);
}
";

    private static Shader? _shader;

    public static void Apply(Creature creature)
    {
        try
        {
            _shader ??= new Shader { Code = ShaderCode };
            CreatureShaderMaterial.Apply(creature, new ShaderMaterial { Shader = _shader });
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply prism look: {ex}");
        }
    }
}
