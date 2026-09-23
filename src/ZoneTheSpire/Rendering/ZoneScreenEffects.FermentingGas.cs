namespace ZoneTheSpire.Rendering;

internal static partial class ZoneScreenEffects
{
    // The Fermentory: now and then a pipe bursts somewhere off a screen edge. A cartoon starburst pops at the break, then a
    // powerful jet of brightly coloured steam blasts inward: chunky round puffs that shoot out fast, slow down and swell as they
    // go, drawn like a cartoon (a dark outline, three flat cel tones lit from above, a white-hot core near the nozzle while it
    // blows, white speed lines racing along it). Every puff is its own bubble with its own highlight crescent, outlined only
    // on the cloud's silhouette, the classic cartoon cloud. After a second or so the jet cuts off: the cloud drifts on, rises
    // a little, breaks into bubbles and poofs away. Each burst picks its own edge, angle, force and colour, and some cycles
    // stay quiet, so it feels random rather than rhythmic.
    private const string FermentingGasShader = @"shader_type canvas_item;

uniform float intensity = 1.0;

const int BURSTS = 3;
const int PUFFS = 60;
const float ACTIVE = 3.8;

vec3 hash31(float n) {
    vec3 p3 = fract(vec3(n) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

vec3 steam_colour(float pick) {
    if (pick < 0.2) return vec3(0.40, 0.92, 0.35);  // toxic green
    if (pick < 0.4) return vec3(0.72, 0.42, 1.00);  // violet
    if (pick < 0.6) return vec3(1.00, 0.70, 0.18);  // amber
    if (pick < 0.8) return vec3(0.25, 0.85, 0.90);  // teal
    return vec3(1.00, 0.42, 0.70);                  // pink
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 p = UV * vec2(aspect, 1.0);
    vec2 light_dir = normalize(vec2(-0.35, -1.0));

    vec4 result = vec4(0.0);
    for (int b = 0; b < BURSTS; b++) {
        float fb = float(b);
        float period = 9.0 + fb * 3.3;
        float t = TIME + fb * 4.1;
        float cycle = floor(t / period);
        float lt = t - cycle * period;
        if (lt > ACTIVE) continue;

        vec3 h = hash31(cycle * 17.13 + fb * 53.71);
        vec3 h2 = hash31(cycle * 7.77 + fb * 91.3 + 3.0);
        if (h2.x < 0.3) continue; // a quiet cycle

        // The break: a spot on an edge (the sides more often than top and bottom), aimed inward at a slight angle.
        float edge = h.x;
        vec2 origin;
        vec2 dir;
        float along = 0.2 + 0.6 * h.y;
        if (edge < 0.35) { origin = vec2(0.0, along); dir = vec2(1.0, 0.0); }
        else if (edge < 0.7) { origin = vec2(aspect, along); dir = vec2(-1.0, 0.0); }
        else if (edge < 0.85) { origin = vec2(along * aspect, 1.0); dir = vec2(0.0, -1.0); }
        else { origin = vec2(along * aspect, 0.0); dir = vec2(0.0, 1.0); }
        float angle = (h.z - 0.5) * 0.7;
        dir = vec2(dir.x * cos(angle) - dir.y * sin(angle), dir.x * sin(angle) + dir.y * cos(angle));
        vec2 perp = vec2(-dir.y, dir.x);

        float emit_end = 0.9 + 0.5 * h2.y;
        float power = 0.95 + 0.2 * h2.z;
        vec3 base = steam_colour(fract(h.x * 7.3 + h.y * 3.1));

        // Metaball puffs: each emitted in turn while the pipe blows, fast at first, slowing under drag, swelling with age.
        float field = 0.0;
        float blend = 0.0;
        float shade = 0.0;
        float hot = 0.0;
        for (int k = 0; k < PUFFS; k++) {
            float fk = float(k);
            float te = fk * emit_end / float(PUFFS);
            if (lt < te) break;
            float age = lt - te;
            float drag = 3.4;
            vec3 hk = hash31(fk * 3.7 + cycle * 11.0 + fb * 5.0);
            // The jet sputters out: the last puffs leave weaker and smaller, so it tails off instead of cutting dead.
            float sputter = 1.0 - 0.6 * smoothstep(emit_end * 0.6, emit_end, te);
            float u = power * sputter * (0.45 + 0.65 * hk.z) * (1.0 - exp(-age * drag)) / drag + 0.03 * age;
            float spread = (hk.x - 0.5) * 0.42 * u + sin(age * 3.0 + fk) * 0.005;
            vec2 c = origin + dir * u + perp * spread - vec2(0.0, 0.006 * age * age);
            float fade = 1.0 - smoothstep(1.0, 2.7, age);
            float r = (0.013 + 0.075 * u + 0.0075 * age) * (0.7 + 0.6 * hk.y) * (0.55 + 0.45 * sputter) * fade;
            vec2 d = p - c;
            float k0 = clamp(1.0 - dot(d, d) / max(r * r, 0.000001), 0.0, 1.0);
            float f = k0 * k0;
            blend += f;
            if (f > field) {
                // Each bubble is lit on its own, like a cartoon cloud: the front-most (strongest) one shades the pixel.
                field = f;
                shade = dot(d / max(r, 0.0001), light_dir);
                hot = (1.0 - smoothstep(0.0, 0.2, age)) * step(lt, emit_end);
            }
        }

        field = max(field, blend * 0.3);
        float inside = smoothstep(0.1, 0.14, field);
        float outline = inside * (1.0 - smoothstep(0.2, 0.3, field));

        vec3 shadow = base * 0.55;
        vec3 highlight = mix(base, vec3(1.0), 0.55);
        vec3 colour = shade > 0.4 ? highlight : (shade < -0.45 ? shadow : base);
        colour = mix(colour, vec3(1.0, 0.98, 0.9), hot * 0.6);
        colour = mix(colour, base * 0.22, outline);
        float alpha = inside * 0.8;

        // Speed lines along the jet while it blows: a few white streaks racing outward inside the cone.
        vec2 rel = p - origin;
        float s_along = dot(rel, dir);
        float q_across = dot(rel, perp);
        float blowing = step(lt, emit_end) * smoothstep(0.05, 0.15, lt);
        for (int j = 0; j < 4; j++) {
            vec3 hj = hash31(float(j) * 9.1 + cycle * 3.3 + fb);
            float reach = power / 3.4 * 0.8;
            float head = fract(lt * 2.4 + hj.x) * reach;
            float lane = (hj.y - 0.5) * 0.3 * head;
            float streak = (1.0 - smoothstep(0.001, 0.0025, abs(q_across - lane)))
                * smoothstep(head - 0.06, head - 0.015, s_along) * (1.0 - smoothstep(head - 0.01, head, s_along));
            float streak_a = streak * blowing * 0.75;
            colour = mix(colour, vec3(1.0), streak_a);
            alpha = max(alpha, streak_a);
        }

        // The pop at the break: a short cartoon starburst in the first quarter second.
        vec2 fromOrigin = p - origin;
        float dist = length(fromOrigin);
        float pop = 1.0 - smoothstep(0.0, 0.25, lt);
        float spikes = pow(abs(cos(atan(fromOrigin.y, fromOrigin.x) * 5.0 + cycle)), 6.0);
        float star = pop * (1.0 - smoothstep(0.006, 0.015 + 0.03 * spikes, dist));
        colour = mix(colour, vec3(1.0, 0.97, 0.85), star);
        alpha = max(alpha, star * 0.95);

        // Over the bursts drawn so far (straight alpha).
        float out_a = alpha + result.a * (1.0 - alpha);
        result.rgb = out_a > 0.0 ? (colour * alpha + result.rgb * result.a * (1.0 - alpha)) / out_a : result.rgb;
        result.a = out_a;
    }

    COLOR = vec4(result.rgb, result.a * intensity);
}";
}
