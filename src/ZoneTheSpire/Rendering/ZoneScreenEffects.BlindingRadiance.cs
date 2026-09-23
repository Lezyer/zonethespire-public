namespace ZoneTheSpire.Rendering;

internal static partial class ZoneScreenEffects
{
    // Blinding Hallows: judged by divinity. A soft overexposed glare falls from above (god rays that slowly breathe, edges
    // blooming to warm white, a few golden motes drifting down), and in it a handful of vast angelic eyes watch the room. Each
    // eye opens slowly somewhere along the top or upper edges of the screen (never over the middle, where the fight is), turns
    // its pupil towards the centre, blinks now and then, closes to a faint golden slit and fades, then opens again elsewhere.
    // At most five, on slow independent cycles, drawn faintly so the room stays readable. Normal blending (the pupils are dark).
    private const string BlindingRadianceShader = @"shader_type canvas_item;

uniform float intensity = 1.0;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// One angelic eye in pixel space. open: 0 closed .. 1 open. look: pupil offset in eye units. Returns colour and alpha.
vec4 angel_eye(vec2 px, vec2 centre, float width, float open, vec2 look, float presence) {
    vec2 d = (px - centre) / width;
    float lid = open * 0.42 * max(0.0, 1.0 - d.x * d.x);
    float inside = 1.0 - smoothstep(lid - 0.02, lid + 0.02, abs(d.y));
    inside *= step(abs(d.x), 1.0);

    // A thin golden line along the lids: all that is left of a closed eye.
    float lash = (1.0 - smoothstep(0.0, 0.035, abs(abs(d.y) - lid))) * step(abs(d.x), 1.0) * (1.0 - abs(d.x) * 0.6);

    vec2 pupil_centre = look * 0.28 * open;
    float r = length(d - pupil_centre);
    float iris = 1.0 - smoothstep(0.26, 0.29, r);
    float iris_rings = 0.5 + 0.5 * sin(r * 70.0);
    float pupil = 1.0 - smoothstep(0.09, 0.12, r);
    float glint = 1.0 - smoothstep(0.0, 0.05, length(d - pupil_centre - vec2(-0.08, -0.08)));

    // A faint halo ring and a few thin rays around the eye.
    float ring = (1.0 - smoothstep(0.0, 0.04, abs(length(d * vec2(1.0, 1.6)) - 1.28))) * 0.5;
    float angle = atan(d.y, d.x);
    float spokes = pow(0.5 + 0.5 * sin(angle * 12.0 + TIME * 0.2), 12.0) * (1.0 - smoothstep(1.1, 2.1, length(d))) * step(1.05, length(d * vec2(1.0, 1.6)));

    vec3 sclera = vec3(1.0, 0.95, 0.84);
    vec3 gold = mix(vec3(0.85, 0.58, 0.16), vec3(1.0, 0.86, 0.45), iris_rings);
    vec3 color = mix(sclera, gold, iris);
    color = mix(color, vec3(0.07, 0.04, 0.02), pupil);
    color = mix(color, vec3(1.0), glint * inside);
    float alpha = inside * (0.5 + 0.35 * iris);
    alpha = max(alpha, pupil * inside * 0.85);

    vec3 halo_col = vec3(1.0, 0.88, 0.55);
    float halo_a = (ring + spokes * 0.35) * open + lash * 0.55;
    color = mix(halo_col, color, clamp(alpha / max(alpha + halo_a, 0.0001), 0.0, 1.0));
    alpha = clamp(alpha + halo_a * (1.0 - alpha), 0.0, 1.0);
    return vec4(color, alpha * presence);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 uv = UV;
    vec2 px = uv * res;

    // The glare from above.
    vec2 d = uv - vec2(0.5, -0.15);
    float angle = atan(d.x, d.y);
    float rays = pow(0.5 + 0.5 * sin(angle * 14.0 + sin(TIME * 0.15) * 2.0), 4.0);
    rays *= pow(0.5 + 0.5 * sin(angle * 5.0 - TIME * 0.1), 2.0);
    rays *= (1.0 - smoothstep(0.0, 1.2, length(d))) * (0.75 + 0.25 * sin(TIME * 0.5));
    float edge = smoothstep(0.35, 0.75, length((uv - 0.5) * vec2(1.0, 1.3)));
    float bloom = edge * (0.16 + 0.04 * sin(TIME * 0.7));
    vec2 cell = floor(px / 26.0 + vec2(0.0, -TIME * 1.2));
    vec2 local = fract(px / 26.0 + vec2(0.0, -TIME * 1.2)) - 0.5;
    float h = hash(cell);
    float mote = step(0.95, h) * (1.0 - smoothstep(0.0, 0.12, length(local))) * (0.5 + 0.5 * sin(TIME * 2.0 + h * 40.0));
    float light = rays * 0.16 + bloom + mote * 0.4;
    vec3 color = vec3(1.0, 0.9, 0.62);
    float alpha = clamp(light, 0.0, 0.6);

    // The watching eyes.
    vec2 watched = vec2(0.5, 0.62) * res;
    for (int i = 0; i < 5; i++) {
        float fi = float(i);
        float period = 11.0 + fi * 2.3;
        float t = TIME + fi * 4.7;
        float cycle = floor(t / period);
        float ph = fract(t / period);
        vec2 hp = vec2(hash(vec2(fi, cycle)), hash(vec2(cycle + 3.1, fi * 7.0)));
        float x = mix(0.06, 0.94, hp.x);
        float y = abs(x - 0.5) < 0.22 ? mix(0.05, 0.13, hp.y) : mix(0.06, 0.34, hp.y);
        vec2 centre = vec2(x, y) * res;
        float width = mix(0.035, 0.06, hash(vec2(fi * 3.3, cycle + 1.0))) * res.x;
        if (length(px - centre) > width * 2.2) {
            continue;
        }
        float open = smoothstep(0.08, 0.3, ph) * (1.0 - smoothstep(0.62, 0.86, ph));
        open *= smoothstep(0.0, 0.03, abs(ph - 0.47));
        float presence = smoothstep(0.0, 0.08, ph) * (1.0 - smoothstep(0.9, 1.0, ph)) * 0.7;
        vec2 look = normalize(watched - centre + vec2(0.001));
        look.x += 0.25 * sin(TIME * 0.35 + fi);
        vec4 e = angel_eye(px, centre, width, open, look, presence);
        color = mix(color, e.rgb, e.a);
        alpha = max(alpha, e.a);
    }

    COLOR = vec4(color, alpha * intensity);
}
";
}
