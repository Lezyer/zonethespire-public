namespace ZoneTheSpire.Rendering;

internal static partial class ZoneScreenEffects
{
    // Deva's Domain: a golden mandala turning slowly in each corner of the screen (petal rings and a fine ring of spokes, each
    // corner at its own speed and direction), with flecks of gold light drifting upwards between them, and every so often a
    // violet ripple spreading out from one of them across the screen and fading. Additive, and faint over the middle so the
    // room and the cards stay readable.
    private const string GoldenMandalasShader = @"shader_type canvas_item;
render_mode blend_add;

uniform float intensity = 1.0;

// TAU is a built-in shader constant in Godot 4; redefining it fails compilation (the overlay then draws plain white).

vec3 hash32(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

// One mandala: concentric petal rings and a ring of fine spokes, turning around its own centre.
float mandala(vec2 p, float spin) {
    float r = length(p);
    if (r > 0.42) {
        return 0.0;
    }
    float a = atan(p.y, p.x) + spin;
    float fade = 1.0 - smoothstep(0.08, 0.42, r);

    // Petal rings: eight petals on the inner ring, sixteen on the outer one.
    float inner = cos(a * 8.0) * 0.5 + 0.5;
    float outer = cos(a * 16.0 - spin * 2.0) * 0.5 + 0.5;
    float ring1 = exp(-pow((r - 0.14 - inner * 0.035) * 46.0, 2.0));
    float ring2 = exp(-pow((r - 0.27 - outer * 0.03) * 40.0, 2.0));

    // Fine spokes and two plain rings holding the pattern together.
    float spokes = pow(abs(cos(a * 24.0)), 26.0) * smoothstep(0.1, 0.34, r) * (1.0 - smoothstep(0.34, 0.4, r));
    float hoop1 = exp(-pow((r - 0.2) * 150.0, 2.0));
    float hoop2 = exp(-pow((r - 0.36) * 120.0, 2.0));
    float core = exp(-pow(r * 22.0, 2.0)) * 0.6;

    return (ring1 * 0.85 + ring2 * 0.7 + spokes * 0.5 + hoop1 * 0.35 + hoop2 * 0.3 + core) * fade;
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 p = (UV - 0.5) * vec2(aspect, 1.0);

    // A mandala in each corner, each turning at its own speed and direction.
    vec2 corner = vec2(aspect * 0.5, 0.5);
    float pattern = 0.0;
    pattern += mandala(p + corner, TIME * 0.05);
    pattern += mandala(p - corner, -TIME * 0.043);
    pattern += mandala(p + vec2(corner.x, -corner.y), -TIME * 0.062);
    pattern += mandala(p - vec2(corner.x, -corner.y), TIME * 0.055);

    // A slow breath over the whole pattern.
    pattern *= 0.72 + 0.28 * sin(TIME * 0.5);

    // Flecks of gold light drifting upwards.
    vec2 cell = floor(vec2(UV.x * 16.0, UV.y * 9.0 + TIME * 0.06));
    vec3 h = hash32(cell);
    vec2 local = fract(vec2(UV.x * 16.0, UV.y * 9.0 + TIME * 0.06)) - vec2(0.5) - (h.yz - 0.5) * 0.6;
    float twinkle = pow(max(0.0, sin(TIME * (0.8 + h.y) + h.z * TAU)), 10.0);
    float motes = step(0.86, h.x) * exp(-pow(length(local * vec2(1.6, 1.0)) * 9.0, 2.0)) * twinkle;

    // Every so often, a violet ripple spreads out from one corner and fades.
    float period = 11.0;
    float cycle = TIME / period;
    vec3 rh = hash32(vec2(floor(cycle), 4.7));
    vec2 origin = corner * (step(0.5, rh.xy) * 2.0 - 1.0);
    float t = fract(cycle);
    float radius = t * 1.9;
    float d = abs(length(p - origin) - radius);
    float ripple = exp(-pow(d * 13.0, 2.0)) * (1.0 - smoothstep(0.55, 1.0, t)) * smoothstep(0.0, 0.12, t);

    vec3 gold = vec3(1.0, 0.78, 0.36);
    vec3 violet = vec3(0.62, 0.38, 0.95);
    vec3 color = gold * (pattern * 0.5 + motes * 0.9) + violet * ripple * 0.5;

    COLOR = vec4(color * intensity, 1.0);
}
";
}
