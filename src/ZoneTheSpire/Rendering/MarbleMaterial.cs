using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Forgotten Empire: enemies with Marbled look like marble statues. The body's brightness is mapped onto a grey-to-ivory stone
/// ramp with dark branching veins and a slow pale sheen. The pattern uses the mesh's local position (not UVs, which jump
/// between Spine atlas pieces), so veins run smoothly across the whole body. Local rendering only; never affects gameplay.
/// </summary>
internal static class MarbleMaterial
{
    private const string ShaderCode = @"shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 0.86;

varying vec2 local_pos;

void vertex() {
    local_pos = VERTEX;
}

vec3 hash32(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

float gnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = dot(hash32(i).xy * 2.0 - 1.0, f);
    float b = dot(hash32(i + vec2(1.0, 0.0)).xy * 2.0 - 1.0, f - vec2(1.0, 0.0));
    float c = dot(hash32(i + vec2(0.0, 1.0)).xy * 2.0 - 1.0, f - vec2(0.0, 1.0));
    float d = dot(hash32(i + vec2(1.0, 1.0)).xy * 2.0 - 1.0, f - vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    mat2 rotation = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++) {
        value += amplitude * gnoise(p);
        p = rotation * p * 2.02 + vec2(1.7, 9.2);
        amplitude *= 0.5;
    }
    return value;
}

void fragment() {
    vec4 base = COLOR;
    float luma = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec2 p = local_pos / 150.0;
    float n = fbm(p);
    float n2 = fbm(p * 2.1 + vec2(n * 1.6, 3.7));
    // Veins where the warped noise crosses zero: a bold branching vein and a finer network.
    float vein = 1.0 - smoothstep(0.0, 0.04, abs(n + 0.45 * n2));
    float fine = 1.0 - smoothstep(0.0, 0.022, abs(n2 - 0.15));
    float l = clamp(luma * 1.1 + 0.1, 0.0, 1.0);
    vec3 shadow = vec3(0.4, 0.4, 0.42);
    vec3 mid = vec3(0.8, 0.79, 0.76);
    vec3 light = vec3(0.98, 0.97, 0.94);
    vec3 marble = l < 0.5 ? mix(shadow, mid, l * 2.0) : mix(mid, light, (l - 0.5) * 2.0);
    marble *= 0.95 + 0.05 * (fbm(p * 5.0) + 0.5);
    marble = mix(marble, vec3(0.34, 0.34, 0.38), clamp(vein * 0.55 + fine * 0.22, 0.0, 1.0));
    vec3 color = mix(base.rgb, marble, strength);
    float band = fract((local_pos.x * 0.6 - local_pos.y) / 700.0 - TIME * 0.08);
    float sheen = smoothstep(0.0, 0.06, band) * (1.0 - smoothstep(0.06, 0.16, band));
    color += vec3(1.0, 0.99, 0.95) * sheen * 0.16;
    COLOR = vec4(color, base.a);
}
";

    private static Shader? _shader;

    public static void Apply(Creature creature) =>
        CreatureShaderMaterial.ApplyWhenReady(creature, () => new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } });
}
