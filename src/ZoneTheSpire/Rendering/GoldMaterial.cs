using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Halls of Midas: a polished gold look for Touch of Midas enemies. The body's brightness is mapped onto a bronze-to-gold
/// ramp and a bright metallic glint sweeps across it. The pattern uses the mesh's local position (not UVs, which jump between
/// Spine atlas pieces), so the glint stays one smooth band. Local rendering only; never affects gameplay.
/// </summary>
internal static class GoldMaterial
{
    private const string ShaderCode = @"shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 0.72;
uniform float sheen_speed = 0.22;
uniform float sheen_width = 520.0;

varying vec2 local_pos;

void vertex() {
    local_pos = VERTEX;
}

void fragment() {
    vec4 base = COLOR;
    float luma = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec3 bronze = vec3(0.28, 0.16, 0.03);
    vec3 gold = vec3(0.88, 0.64, 0.16);
    vec3 pale = vec3(1.0, 0.95, 0.68);
    vec3 metal = luma < 0.5 ? mix(bronze, gold, luma * 2.0) : mix(gold, pale, (luma - 0.5) * 2.0);
    // Slow, broad shimmer so the gold never looks flat.
    metal *= 0.92 + 0.08 * sin((local_pos.x - local_pos.y) / 90.0 + TIME * 1.3);
    vec3 color = mix(base.rgb, metal, strength);
    float band = fract((local_pos.x + local_pos.y) / sheen_width - TIME * sheen_speed);
    float glint = smoothstep(0.0, 0.05, band) * (1.0 - smoothstep(0.05, 0.12, band));
    color += vec3(1.0, 0.9, 0.55) * glint * 0.6;
    COLOR = vec4(color, base.a);
}
";

    private static Shader? _shader;

    public static void Apply(Creature creature) =>
        CreatureShaderMaterial.ApplyWhenReady(creature, () => new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } });
}
