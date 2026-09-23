namespace ZoneTheSpire.Rendering;

internal static partial class ZoneScreenEffects
{
    // Hoarfrost: frost creeping in from the corners of the screen in fine crystal fronds, with snow drifting slowly down across
    // it and the odd flake catching the light. The middle of the screen stays clear. Normal blending for the frost (it is pale
    // and sits over the room), with the snow added on top.
    private const string DriftingFrostShader = @"shader_type canvas_item;

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
        p = rotation * p * 2.03 + vec2(1.7, 9.2);
        amplitude *= 0.5;
    }
    return value;
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 p = (UV - 0.5) * vec2(aspect, 1.0);

    // Frost grows from the corners: strongest where both axes are far from the middle.
    vec2 corner_dist = abs(p) / vec2(aspect * 0.5, 0.5);
    float corner = clamp(corner_dist.x * corner_dist.y, 0.0, 1.0);
    float creep = smoothstep(0.35, 1.0, corner);

    // Crystal fronds: ridged noise, spreading very slowly.
    vec2 q = p * 4.5;
    float ridge = 1.0 - abs(fbm(q + vec2(0.0, TIME * 0.012)) * 2.0);
    float fronds = pow(clamp(ridge, 0.0, 1.0), 5.0);
    float breath = 0.82 + 0.18 * sin(TIME * 0.35);
    float frost = clamp(creep * (0.5 + fronds * 1.3) * breath, 0.0, 1.0);

    // Snow: three layers of flakes falling at their own speeds, drifting sideways as they go.
    float snow = 0.0;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        float scale = 26.0 + fi * 16.0;
        float speed = 0.05 + fi * 0.035;
        // The sampling grid moves up the screen, which is what makes the flakes themselves fall.
        vec2 grid = vec2(UV.x * aspect * scale + sin(TIME * 0.25 + fi) * 0.6, UV.y * scale - TIME * speed * scale * 0.1);
        vec2 cell = floor(grid);
        vec3 h = hash32(cell + fi * 31.0);
        vec2 local = fract(grid) - vec2(0.5) - (h.xy - 0.5) * 0.7;
        float flake = step(0.88, h.z) * (1.0 - smoothstep(0.015, 0.09, length(local)));
        snow += flake * (0.35 + fi * 0.2);
    }

    vec3 ice = vec3(0.72, 0.87, 0.98);
    vec3 color = ice * (0.55 + fronds * 0.45);
    float alpha = frost * 0.5;
    color += vec3(1.0) * snow * 0.7;
    alpha = max(alpha, snow * 0.55);

    COLOR = vec4(color, clamp(alpha * intensity, 0.0, 0.7));
}
";
}
