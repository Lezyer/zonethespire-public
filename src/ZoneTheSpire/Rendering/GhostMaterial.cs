using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Rendering;

internal enum GhostStrength
{
    /// <summary>Phantasm enemies: see-through, green and softly shimmering.</summary>
    Phantasm,

    /// <summary>Ghostly copies: much fainter, paler and more flickery.</summary>
    Spectre,
}

/// <summary>
/// Phantasmal Tombs: a translucent greenish ghost look for creature bodies. Brightness is mapped onto a ghostly green ramp,
/// the body becomes see-through, and a slow wave and flicker run through it (using mesh-local positions, which stay smooth
/// across Spine atlas pieces). Local rendering only; never affects gameplay.
/// </summary>
internal static class GhostMaterial
{
    private const string ShaderCode = @"shader_type canvas_item;

uniform float opacity : hint_range(0.0, 1.0) = 0.7;
uniform float tint : hint_range(0.0, 1.0) = 0.55;
uniform float flicker = 0.08;

varying vec2 local_pos;

void vertex() {
    local_pos = VERTEX;
}

void fragment() {
    vec4 base = COLOR;
    float luma = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec3 ghost = mix(vec3(0.16, 0.45, 0.34), vec3(0.8, 1.0, 0.9), luma);
    vec3 color = mix(base.rgb, ghost * (0.75 + 0.5 * luma), tint);
    float wave = 0.82 + 0.18 * sin(local_pos.y / 38.0 - TIME * 2.2);
    float flick = 1.0 - flicker + flicker * sin(TIME * 7.0 + local_pos.x / 55.0);
    COLOR = vec4(color * (0.9 + 0.2 * wave), base.a * opacity * wave * flick);
}
";

    private static Shader? _shader;

    public static void Apply(Creature creature, GhostStrength strength) =>
        CreatureShaderMaterial.ApplyWhenReady(creature, () =>
        {
            var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
            bool spectre = strength == GhostStrength.Spectre;
            material.SetShaderParameter("opacity", spectre ? 0.42f : 0.72f);
            material.SetShaderParameter("tint", spectre ? 0.8f : 0.55f);
            material.SetShaderParameter("flicker", spectre ? 0.2f : 0.08f);
            return material;
        });
}
