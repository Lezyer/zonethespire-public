namespace ZoneTheSpire.Rendering;

internal static partial class CardModifierVisuals
{
    // Biting Cold: a cold blue rim with slow frost crystals growing in from the corners, and a faint drifting glitter. Additive
    // and kept faint over the middle so the card stays readable.
    private const string BitingColdShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
void fragment() {
    vec2 centered = UV - 0.5;
    float edge = smoothstep(0.30, 0.5, max(abs(centered.x), abs(centered.y)));

    // Frost crystals: ridged noise that is densest in the corners and creeps slowly inwards.
    vec2 p = UV * rect_size / 60.0;
    float ridge = 1.0 - abs(fbm(p + vec2(0.0, TIME * 0.02)) * 2.0);
    float crystals = pow(clamp(ridge, 0.0, 1.0), 6.0) * (0.25 + edge * 1.4);

    // A slow breath of cold over the whole card, and a few glints in the frost.
    float breath = 0.75 + 0.25 * sin(TIME * 0.6);
    vec3 h = hash32(floor(UV * rect_size / 22.0));
    vec2 local = fract(UV * rect_size / 22.0) - 0.5;
    float glint = step(0.93, h.x) * (1.0 - smoothstep(0.02, 0.14, length(local)))
        * pow(max(0.0, sin(TIME * (1.2 + h.y) + h.z * 30.0)), 14.0);

    vec3 ice = vec3(0.62, 0.85, 1.0);
    float glow = (edge * 0.20 + crystals * 0.34 + glint * 0.7) * breath;
    COLOR = vec4(ice * glow * card_mask(UV), 1.0);
}
";

    // Frozen (Hoarfrost fights): the card is behind a sheet of ice. The frost is thick around the frame and thin over the middle,
    // so the card's art and text stay readable while it still reads as iced over at a glance: crystals grow in from the edges, a
    // sheen slides across the sheet, and a few bright cracks catch the light.
    private const string FrozenShader = @"shader_type canvas_item;
" + SharedFunctions + @"
void fragment() {
    vec2 centered = UV - 0.5;
    // Thick at the frame, thin over the middle, so the art and text read through the ice.
    float edge = smoothstep(0.16, 0.5, max(abs(centered.x), abs(centered.y)));
    float rim = smoothstep(0.34, 0.5, max(abs(centered.x), abs(centered.y)));

    // Crystals growing in from the frame, and a slow sheen crossing the sheet.
    vec2 p = UV * rect_size / 70.0;
    float ridge = 1.0 - abs(fbm(p + vec2(TIME * 0.01, 0.0)) * 2.0);
    float crystals = pow(clamp(ridge, 0.0, 1.0), 4.0) * (0.25 + edge * 1.2);
    float sheen = exp(-pow((UV.x + UV.y * 0.7 - fract(TIME * 0.12) * 2.4 + 0.5) * 7.0, 2.0));

    // A few bright cracks running through the ice, fixed per card so the sheet looks solid.
    float cracks = pow(clamp(1.0 - abs(fbm(p * 1.7 + vec2(4.3, 1.1)) * 2.4), 0.0, 1.0), 16.0);

    vec3 ice = vec3(0.62, 0.82, 0.96);
    vec3 color = ice + vec3(0.88, 0.96, 1.0) * (crystals * 0.4 + sheen * 0.3 + cracks * 0.5);
    float alpha = 0.16 + rim * 0.34 + crystals * 0.26 + cracks * 0.25 + sheen * 0.06;
    COLOR = vec4(color, clamp(alpha, 0.0, 0.66) * card_mask(UV));
}
";
}
