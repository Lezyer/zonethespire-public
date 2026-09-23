namespace ZoneTheSpire.Rendering;

internal static partial class ZoneScreenEffects
{
    // Forgotten Empire: weathered, veined stone creeping in from the screen edges; pale stone flakes tumbling slowly down,
    // thinning edge-on and catching the light as they turn; and motes of light rising like dust in an old temple's sunbeam.
    private const string CrumblingRuinsShader = @"shader_type canvas_item;

uniform float intensity = 1.0;

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

// One stone flake centred on d = 0: x = coverage (antialiased), y = which of its two faces is lit.
vec2 flake(vec2 d, float angle, vec2 half_size, float skew) {
    float c = cos(angle);
    float s = sin(angle);
    vec2 r = vec2(c * d.x - s * d.y, s * d.x + c * d.y);
    r.x += r.y * skew;
    vec2 q = abs(r) / half_size;
    float k = max(q.x, q.y) * 0.55 + (q.x + q.y) * 0.45;
    // fwidth jumps where fract() wraps at a cell border; capping it keeps those borders from drawing faint lines.
    float aa = min(fwidth(k) * 1.5, 0.12) + 0.001;
    float cover = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, k);
    float face = step(0.0, r.x * 0.7 + r.y);
    return vec2(cover, face);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 uv = vec2(UV.x * aspect, UV.y);

    vec3 color = vec3(0.86, 0.86, 0.84);
    float alpha = 0.0;

    // Weathered stone creeping in from the edges, strongest in the corners, with slowly shifting dark veins.
    vec2 centred = (UV - 0.5) * vec2(aspect, 1.0);
    float edge = smoothstep(0.55, 1.15, length(centred * vec2(0.72, 1.0)) * 1.2);
    float grain = fbm(uv * 5.0 + vec2(2.3, 7.1)) + 0.5;
    float drift = fbm(uv * 2.2 + vec2(TIME * 0.006, -TIME * 0.004));
    float veins = 1.0 - smoothstep(0.0, 0.02, abs(drift + 0.25 * fbm(uv * 7.0)));
    alpha += edge * (0.08 + 0.06 * grain);
    color = mix(color, vec3(0.36, 0.36, 0.39), veins * edge * 0.9);
    alpha += veins * edge * 0.1;

    // Tumbling flakes drifting down: a far layer of small faint ones and a near layer of larger ones. Each cell holds at most
    // one flake that stays well inside the cell, so none is ever cut off.
    for (int layer = 0; layer < 2; layer++) {
        float fl = float(layer);
        float scale = mix(9.0, 4.5, fl);
        float speed = mix(0.16, 0.28, fl);
        vec2 p = uv * scale - vec2(TIME * 0.05 * (1.0 + fl), TIME * speed);
        vec2 cell = floor(p);
        vec3 h = hash32(cell + vec2(17.0 * fl, 3.0));
        if (h.z > mix(0.8, 0.86, fl)) {
            vec2 center = vec2(0.5) + (h.xy - 0.5) * 0.3 + vec2(0.08 * sin(TIME * 0.7 + h.x * 40.0), 0.0);
            float spin = (h.y > 0.5 ? 1.0 : -1.0) * (0.5 + 1.4 * h.x);
            float angle = TIME * spin + h.z * 50.0;
            vec2 half_size = vec2(0.07 + 0.04 * h.y, 0.045 + 0.03 * h.x) * mix(1.0, 1.2, fl);
            // Flat flakes look thinner edge-on as they tumble.
            float tumble = abs(cos(TIME * (0.8 + h.y) + h.x * 30.0));
            half_size.y *= 0.3 + 0.7 * tumble;
            vec2 f = flake(fract(p) - center, angle, half_size, (h.x - 0.5) * 0.6);
            float glint = smoothstep(0.94, 1.0, tumble) * f.y;
            vec3 stone = mix(vec3(0.56, 0.56, 0.56), vec3(0.94, 0.93, 0.9), f.y * (0.5 + 0.5 * tumble));
            stone += vec3(1.0) * glint * 0.35;
            float a = f.x * mix(0.3, 0.52, fl);
            color = mix(color, stone, a);
            alpha = max(alpha, a);
        }
    }

    // Motes of pale light rising slowly and twinkling.
    vec2 mp = uv * 14.0 + vec2(0.0, TIME * 0.12);
    vec3 mh = hash32(floor(mp) + vec2(91.0, 47.0));
    if (mh.z > 0.86) {
        vec2 mc = vec2(0.5) + (mh.xy - 0.5) * 0.5 + vec2(0.08 * sin(TIME * 0.9 + mh.x * 20.0), 0.0);
        float twinkle = max(0.0, 0.45 + 0.55 * sin(TIME * (1.2 + mh.y * 1.5) + mh.x * 60.0));
        float mote = (1.0 - smoothstep(0.0, 0.08, length(fract(mp) - mc))) * twinkle;
        color = mix(color, vec3(1.0, 0.99, 0.95), mote);
        alpha = max(alpha, mote * 0.4);
    }

    COLOR = vec4(color, clamp(alpha, 0.0, 0.6) * intensity);
}
";
}
