using Godot;

namespace ZoneTheSpire.Rendering;

internal static partial class CardModifierVisuals
{
    private static ShaderMaterial? _hallowingMaterial;
    private static ShaderMaterial? _blasphemousMaterial;
    private static ShaderMaterial? _blasphemousMarkMaterial;
    private static ShaderMaterial? _redemptionMaterial;

    // Holy light, shared by all three: a thin faint halo near the top, soft rays falling from above the card, a pale bloom from
    // the upper middle, and a few drifting motes of light. Near-white with a faint warm tint: no metallic gold, no rim and no
    // sweeping glint, so it never reads as Gilded. Red veins (Blasphemous): ridged noise, thick at the frame and thinning
    // towards the middle, with a slow crimson pulse running outwards along it.
    private const string HolyFunctions = @"
float holy_light(vec2 uv) {
    vec2 px = uv * rect_size;
    vec2 halo_p = (px - vec2(rect_size.x * 0.5, rect_size.y * 0.075)) / rect_size.x;
    float halo = 1.0 - smoothstep(0.0, 0.012, abs(length(halo_p * vec2(1.0, 3.2)) - 0.22));
    vec2 d = uv - vec2(0.5, -0.25);
    float angle = atan(d.x, d.y);
    float rays = pow(0.5 + 0.5 * sin(angle * 18.0 + sin(TIME * 0.3) * 1.5), 5.0) * (1.0 - smoothstep(0.2, 1.15, uv.y));
    vec2 b = (uv - vec2(0.5, 0.3)) * vec2(1.6, 1.2);
    float bloom = exp(-dot(b, b) * 6.0);
    vec2 drift = px / 26.0 + vec2(0.0, -TIME * 0.6);
    vec3 mh = hash32(floor(drift));
    float mote = step(0.93, mh.x) * (1.0 - smoothstep(0.02, 0.16, length(fract(drift) - 0.5)))
        * (0.5 + 0.5 * sin(TIME * 2.0 + mh.y * 30.0));
    float breath = 0.85 + 0.15 * sin(TIME * 0.9);
    return (halo * 0.5 + rays * 0.12 + bloom * 0.14 + mote * 0.5) * breath;
}

float red_veins(vec2 uv) {
    float ridge = 1.0 - abs(fbm(uv * rect_size / 55.0 + vec2(3.1, 7.4)) * 2.0);
    float veins = pow(clamp(ridge, 0.0, 1.0), 10.0);
    float edge = smoothstep(0.12, 0.5, max(abs(uv.x - 0.5), abs(uv.y - 0.5)));
    float pulse = 0.6 + 0.4 * sin(TIME * 1.8 - length(uv - 0.5) * 12.0);
    return clamp(veins * (0.2 + edge * 1.1) * pulse, 0.0, 1.0);
}

// Holy light with dark crimson veins through it. Normal blending (not additive), so the veins read as dark red over bright
// card art instead of just brightening it.
vec4 blasphemous(vec2 uv) {
    float light = holy_light(uv);
    float veins = red_veins(uv);
    vec3 col = mix(vec3(1.0, 0.95, 0.85), vec3(0.62, 0.02, 0.04), clamp(veins * 1.6, 0.0, 1.0));
    float alpha = clamp(light * 0.75 + veins * 0.7, 0.0, 0.8);
    return vec4(col, alpha * card_mask(uv));
}
";

    // Hallowing: an ornate divine eye in the top-right corner and golden angelic veins growing up from the bottom of the card.
    // The eye sits in a soft golden bloom with a slowly turning sunburst behind it and a thin halo ring with eight small lights
    // orbiting it; it has shaded ivory whites, a gold-to-amber iris with fine radial striations and a glowing rim, a breathing
    // pupil and a star-shaped catchlight, and it blinks every few seconds and glances around. The veins only cover the bottom
    // of the card, thickest at the bottom edge, with light pulsing upwards along them; the noise is rotated and domain-warped
    // (so no vein can run along a straight noise-grid line) and each vein has an anti-aliased soft edge. Normal blending (dark
    // pupil). Blasphemous is holy light with red veins all around and no eye; Gilded is a gold rim with a sweeping glint.
    private const string HallowingShader = @"shader_type canvas_item;
" + SharedFunctions + @"
// Porter-Duff 'over': composite a colour with alpha on top of what is there.
vec4 over(vec4 dst, vec3 c, float a) {
    a = clamp(a, 0.0, 1.0);
    float out_a = a + dst.a * (1.0 - a);
    vec3 out_c = (c * a + dst.rgb * dst.a * (1.0 - a)) / max(out_a, 0.0001);
    return vec4(out_c, out_a);
}

vec4 divine_eye(vec2 uv) {
    vec2 px = uv * rect_size;
    float scale = rect_size.x * 0.095;
    vec2 centre = vec2(rect_size.x * 0.79, rect_size.y * 0.095);
    vec2 d = (px - centre) / scale;
    float dist = length(d);
    if (dist > 2.3) {
        return vec4(0.0);
    }
    float angle = atan(d.y, d.x);
    vec4 acc = vec4(0.0);

    // Soft golden bloom and a slowly turning sunburst behind the eye.
    acc = over(acc, vec3(1.0, 0.88, 0.55), exp(-dist * dist * 1.1) * 0.35);
    float rays = pow(0.5 + 0.5 * sin(angle * 16.0 + TIME * 0.25), 10.0) * (1.0 - smoothstep(0.9, 2.2, dist)) * smoothstep(0.65, 1.0, dist);
    acc = over(acc, vec3(1.0, 0.9, 0.6), rays * 0.5);

    // A thin halo ring with eight small lights orbiting it.
    float ring = 1.0 - smoothstep(0.0, 0.05, abs(dist - 1.45));
    float dots = 0.0;
    for (int i = 0; i < 8; i++) {
        float a = float(i) * 0.785398 + TIME * 0.35;
        dots = max(dots, 1.0 - smoothstep(0.05, 0.11, length(d - vec2(cos(a), sin(a)) * 1.45)));
    }
    acc = over(acc, vec3(1.0, 0.85, 0.45), ring * 0.55 + dots * 0.9);

    // The eye.
    float cycle = fract(TIME / 5.5);
    float blink = smoothstep(0.0, 0.035, abs(cycle - 0.93));
    float open = (0.85 + 0.15 * sin(TIME * 0.9)) * blink;
    float lid = open * 0.45 * max(0.0, 1.0 - d.x * d.x);
    float inside = (1.0 - smoothstep(lid - 0.03, lid + 0.03, abs(d.y))) * step(abs(d.x), 1.0);
    float lid_line = (1.0 - smoothstep(0.02, 0.07, abs(abs(d.y) - lid))) * step(abs(d.x), 1.02);
    vec2 look = vec2(0.32 * sin(TIME * 0.45), 0.08 * sin(TIME * 0.7 + 1.0)) * open;
    vec2 q = d - look;
    float r = length(q);
    float iris_angle = atan(q.y, q.x);
    vec3 sclera = mix(vec3(1.0, 0.97, 0.9), vec3(0.93, 0.85, 0.7), smoothstep(0.3, 1.0, abs(d.x)));
    float iris = 1.0 - smoothstep(0.28, 0.31, r);
    vec3 iris_col = mix(vec3(1.0, 0.88, 0.45), vec3(0.78, 0.45, 0.1), smoothstep(0.08, 0.3, r));
    iris_col *= 0.85 + 0.3 * pow(0.5 + 0.5 * sin(iris_angle * 36.0), 3.0);
    float limbal = 1.0 - smoothstep(0.0, 0.03, abs(r - 0.29));
    float pupil = 1.0 - smoothstep(0.09, 0.12, r * (1.0 + 0.1 * sin(TIME * 2.0)));
    vec2 g = q - vec2(-0.08, -0.08);
    float glint = (1.0 - smoothstep(0.0, 0.045, length(g)))
        + (1.0 - smoothstep(0.0, 0.012, abs(g.x))) * (1.0 - smoothstep(0.0, 0.14, abs(g.y))) * 0.8
        + (1.0 - smoothstep(0.0, 0.012, abs(g.y))) * (1.0 - smoothstep(0.0, 0.14, abs(g.x))) * 0.8;
    vec3 eye_col = mix(sclera, iris_col, iris);
    eye_col = mix(eye_col, vec3(0.95, 0.7, 0.3), limbal * iris * 0.8);
    eye_col = mix(eye_col, vec3(0.06, 0.03, 0.01), pupil);
    eye_col = mix(eye_col, vec3(1.0), clamp(glint, 0.0, 1.0));
    acc = over(acc, eye_col, inside * 0.95);
    acc = over(acc, vec3(0.95, 0.72, 0.25), lid_line * 0.9);
    return acc;
}

// Golden veins over the bottom of the card only. The noise is rotated off its grid and domain-warped, so no vein can follow
// a straight grid line, and the veins get an anti-aliased soft edge rather than a hard crease.
float gold_veins(vec2 uv) {
    vec2 p = mat2(vec2(0.87, 0.5), vec2(-0.5, 0.87)) * (uv * rect_size / 48.0) + vec2(9.3, 2.1);
    vec2 warp = vec2(fbm(p * 0.6 + vec2(3.1, 7.7)), fbm(p * 0.6 + vec2(8.4, 1.3)));
    float n = fbm(p + warp * 1.3);
    float width = fwidth(n) * 1.2 + 0.035;
    float veins = 1.0 - smoothstep(0.0, width, abs(n));
    float fine = 1.0 - smoothstep(0.0, width * 0.6, abs(fbm(p * 2.1 + warp * 0.8 + vec2(5.2, 3.3))));
    veins = max(veins, fine * 0.45);
    float reach = smoothstep(0.58, 0.98, uv.y);
    float pulse = 0.6 + 0.4 * sin(TIME * 1.6 + uv.y * 14.0);
    return clamp(veins * reach * (0.5 + reach) * pulse * 1.3, 0.0, 1.0);
}

void fragment() {
    float veins = gold_veins(UV);
    float glow = smoothstep(0.8, 1.0, UV.y) * (0.14 + 0.04 * sin(TIME * 0.8));
    vec3 color = mix(vec3(1.0, 0.8, 0.35), vec3(1.0, 0.95, 0.75), veins * 0.6);
    vec4 acc = vec4(color, clamp(veins * 0.85 + glow, 0.0, 0.85));
    vec4 eye = divine_eye(UV);
    acc = over(acc, eye.rgb, eye.a);
    COLOR = vec4(acc.rgb, acc.a * card_mask(UV));
}
";

    // Redemption: soft pearl light breathing in from the edges, and a few white feathers drifting slowly down the card, swaying
    // as they fall. Additive and faint; pearl white with a hint of rose, so it never reads as Biting Cold's blue frost,
    // Hallowing's gold or Gilded's rim.
    private const string RedemptionShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
// One feather in pixel space around its centre: a lens-shaped vane with barbs and a thin shaft, rotated by angle.
float feather(vec2 p, float angle) {
    float c = cos(angle);
    float s = sin(angle);
    vec2 q = vec2(c * p.x + s * p.y, -s * p.x + c * p.y);
    float len = 24.0;
    float t = clamp(q.y / len, -1.0, 1.0);
    float half_w = 7.0 * (1.0 - t * t);
    float vane = (1.0 - smoothstep(half_w - 1.2, half_w + 0.8, abs(q.x))) * (1.0 - smoothstep(len - 2.0, len + 1.0, abs(q.y)));
    float barbs = 0.75 + 0.25 * sin(q.y * 1.6 + abs(q.x) * 2.2);
    float shaft = (1.0 - smoothstep(0.4, 1.2, abs(q.x))) * (1.0 - smoothstep(len, len + 6.0, abs(q.y + 4.0)));
    return max(vane * barbs * 0.8, shaft);
}

void fragment() {
    vec2 px = UV * rect_size;
    float feathers = 0.0;
    for (int i = 0; i < 5; i++) {
        float fi = float(i);
        vec3 h = hash32(vec2(fi * 5.1 + 2.0, 7.3));
        float speed = 18.0 + 10.0 * h.y;
        float span = rect_size.y + 80.0;
        float t = TIME + h.z * span / speed;
        float y = mod(t * speed, span) - 40.0;
        float x = (0.15 + 0.7 * h.x) * rect_size.x + sin(t * 0.9 + fi) * 16.0;
        vec2 d = px - vec2(x, y);
        if (dot(d, d) > 1600.0) {
            continue;
        }
        feathers = max(feathers, feather(d, 0.4 + 0.5 * sin(t * 0.9 + fi)));
    }
    vec2 c = UV - 0.5;
    float edge = smoothstep(0.3, 0.5, max(abs(c.x), abs(c.y)));
    float glow = edge * 0.16 * (0.8 + 0.2 * sin(TIME * 0.7)) + 0.04;
    COLOR = vec4(vec3(1.0, 0.95, 0.97) * (feathers * 0.45 + glow) * card_mask(UV), 1.0);
}
";

    // Blasphemous (permanent modifier): holy light with red veins.
    private const string BlasphemousShader = @"shader_type canvas_item;
" + SharedFunctions + HolyFunctions + @"
void fragment() {
    COLOR = blasphemous(UV);
}
";

    // Blasphemous mark (this turn only): the Blasphemous look plus a pulsing crimson border, so this turn's marks stand out.
    private const string BlasphemousMarkShader = @"shader_type canvas_item;
" + SharedFunctions + HolyFunctions + @"
void fragment() {
    vec4 b = blasphemous(UV);
    vec2 c = UV - 0.5;
    float border = smoothstep(0.42, 0.5, max(abs(c.x), abs(c.y)));
    float glow = border * (0.35 + 0.45 * (0.5 + 0.5 * sin(TIME * 4.0))) * card_mask(UV);
    vec3 col = mix(b.rgb, vec3(0.95, 0.12, 0.08), glow / max(glow + b.a, 0.001));
    COLOR = vec4(col, clamp(b.a + glow, 0.0, 0.9));
}
";
}
