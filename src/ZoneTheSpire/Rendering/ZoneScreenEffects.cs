using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Visuals;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Quiet full-screen ambience for rooms in some zones: falling blood streaks in Blood Rain, drifting rainbow light and
/// twinkles in the Prismatic Storm, golden god rays from the top right in the Halls of Midas, a sweeping mirror shine in
/// Mirrorlands, floating dust clouds in the Scrapyard, buzzing flies in the Infestation and translucent ghostly forms drifting through
/// the Phantasmal Tombs, and a golden glare with slowly blinking angelic eyes in Blinding Hallows. Each is one full-rect ColorRect with a shader, added to the room's node (so it goes
/// away with the room) and, in combat, placed under the combat UI so the hand stays clear. Ignores the mouse. Local rendering
/// only; never affects gameplay.
/// </summary>
internal static partial class ZoneScreenEffects
{
    private const string NodeName = "ZoneTheSpireScreenEffect";
    private const int MaxRetries = 10;

    private const string BloodRainShader = @"shader_type canvas_item;

uniform float intensity = 1.0;

float hash(float n) {
    return fract(sin(n * 12.9898) * 43758.5453);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 p = UV * res;
    float rain = 0.0;
    for (int layer = 0; layer < 3; layer++) {
        float fl = float(layer);
        float column = 34.0 + fl * 22.0;
        float speed = 780.0 - fl * 210.0;
        float len = 64.0 - fl * 18.0;
        vec2 q = vec2(p.x + p.y * 0.12, p.y);
        float id = floor(q.x / column);
        if (hash(id + fl * 57.0) > 0.42) {
            continue;
        }
        float x = (fract(q.x / column) - 0.5) * column + (hash(id * 3.7 + fl) - 0.5) * column * 0.6;
        float period = res.y * (0.9 + hash(id * 7.3 + fl * 3.0) * 1.4);
        float y = mod(q.y - TIME * speed + hash(id * 11.1 + fl) * period, period);
        float streak = step(y, len) * smoothstep(0.0, len, y);
        float thickness = 1.5 - fl * 0.35;
        float line = 1.0 - smoothstep(thickness * 0.5, thickness * 0.5 + 1.0, abs(x));
        rain = max(rain, line * streak * (0.46 - fl * 0.11));
    }
    float mist = smoothstep(0.55, 1.0, UV.y) * (0.11 + 0.03 * sin(TIME * 0.4 + UV.x * 5.0));
    float vignette = smoothstep(0.45, 0.98, length(UV - vec2(0.5))) * 0.16;
    vec3 blood = mix(vec3(0.38, 0.0, 0.03), vec3(0.7, 0.05, 0.09), clamp(rain / 0.46, 0.0, 1.0));
    COLOR = vec4(blood, clamp(rain + mist + vignette, 0.0, 1.0) * intensity);
}
";

    private const string PrismaticLightShader = @"shader_type canvas_item;
render_mode blend_add;

uniform float intensity = 1.0;

vec3 hue(float h) {
    return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 p = UV * res;
    float along = UV.x * 0.8 + UV.y * 0.6;
    float shafts = 0.0;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        float wave = sin(along * (9.0 + fi * 5.0) - TIME * (0.15 + fi * 0.05) + fi * 2.1);
        shafts += smoothstep(0.75, 1.0, wave) * (0.5 - fi * 0.12);
    }
    float fade = (1.0 - UV.y) * 0.9 + 0.1;
    vec3 color = hue(fract(along * 0.8 - TIME * 0.03)) * shafts * 0.11 * fade;

    vec2 drifting = p + vec2(TIME * 6.0, -TIME * 9.0);
    vec2 cell = floor(drifting / 46.0);
    vec2 f = fract(drifting / 46.0);
    float h = hash(cell);
    if (h > 0.8) {
        vec2 center = vec2(hash(cell + 1.3), hash(cell + 2.7)) * 0.6 + 0.2;
        float twinkle = pow(0.5 + 0.5 * sin(TIME * (1.0 + h * 2.0) + h * 40.0), 6.0);
        float d = length(f - center);
        float star = (0.02 / (d + 0.02)) * smoothstep(0.35, 0.0, d);
        float rays = (smoothstep(0.02, 0.0, abs(f.x - center.x)) + smoothstep(0.02, 0.0, abs(f.y - center.y))) * smoothstep(0.25, 0.0, d);
        color += hue(fract(h * 7.0 + TIME * 0.1)) * (star * 0.4 + rays * 0.24) * twinkle;
    }

    float edge = smoothstep(0.55, 1.0, length(UV - vec2(0.5)) * 1.4);
    color += hue(fract(atan(UV.y - 0.5, UV.x - 0.5) / 6.2831 + TIME * 0.02)) * edge * 0.08;
    COLOR = vec4(color * intensity, 1.0);
}
";

    private const string GoldenRaysShader = @"shader_type canvas_item;
render_mode blend_add;

uniform float intensity = 1.0;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 d = (UV - vec2(1.08, -0.12)) * vec2(res.x / res.y, 1.0);
    float dist = length(d);
    float angle = atan(d.y, d.x);
    float rays = pow(0.5 + 0.5 * sin(angle * 22.0 + TIME * 0.25), 6.0) * 0.5
        + pow(0.5 + 0.5 * sin(angle * 37.0 - TIME * 0.18 + 1.3), 8.0) * 0.35
        + pow(0.5 + 0.5 * sin(angle * 9.0 + TIME * 0.1 + 2.2), 4.0) * 0.35;
    float falloff = exp(-dist * 1.3);
    float pulse = 0.85 + 0.15 * sin(TIME * 0.6);
    vec3 color = vec3(1.0, 0.78, 0.32) * (rays * falloff * 0.33 * pulse + exp(-dist * 3.6) * 0.38);

    vec2 p = UV * res + vec2(-TIME * 10.0, TIME * 14.0);
    vec2 cell = floor(p / 60.0);
    vec2 f = fract(p / 60.0);
    float h = hash(cell);
    if (h > 0.84) {
        vec2 center = vec2(hash(cell + 1.9), hash(cell + 5.3)) * 0.6 + 0.2;
        float mote = smoothstep(0.08, 0.0, length(f - center));
        float twinkle = 0.5 + 0.5 * sin(TIME * 1.5 + h * 30.0);
        color += vec3(1.0, 0.86, 0.46) * mote * twinkle * 0.35 * (falloff * 2.0 + 0.15);
    }

    COLOR = vec4(color * intensity, 1.0);
}
";

    private const string MirrorShineShader = @"shader_type canvas_item;
render_mode blend_add;

uniform float intensity = 1.0;
uniform float sweep_seconds = 7.0;

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    float diag = UV.x * aspect * 0.75 + UV.y * 0.55;
    float t = mod(TIME, sweep_seconds) / sweep_seconds;
    float pos = mix(-0.6, aspect * 0.75 + 1.2, t);
    float wide = exp(-pow((diag - pos) / 0.18, 2.0));
    float thin = exp(-pow((diag - pos - 0.28) / 0.035, 2.0));
    float sweep = wide * 0.11 + thin * 0.17;
    float drift = TIME * 0.05;
    float streaks = pow(0.5 + 0.5 * sin(diag * 6.0 + 1.0 + drift), 12.0) * 0.04
        + pow(0.5 + 0.5 * sin(diag * 14.0 + 4.0 - drift), 20.0) * 0.03;
    float edge = smoothstep(0.35, 0.75, length(UV - vec2(0.5))) * 0.07;
    vec3 silver = vec3(0.82, 0.9, 1.0);
    COLOR = vec4(silver * (sweep + streaks + edge) * intensity, 1.0);
}
";

    // Shadow Corruption: living darkness closing in from the screen edges. Domain-warped smoke claws inwards along a few ridged
    // tendrils; the darkness surges in on a slow breath with a faint double heartbeat; a violet rim smoulders where the dark meets
    // the light; wisps rise through the dark; and now and then a pair of violet eyes opens in the gloom, blinks, and closes. The
    // centre of the screen stays clear. Normal blending (it darkens), eyes and rim glow on top.
    private const string CreepingShadowShader = @"shader_type canvas_item;

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
    float r = length(p * vec2(0.82, 1.0));
    float angle = atan(p.y, p.x);

    // Slow breath with a faint double heartbeat on top.
    float breath = 0.5 + 0.5 * sin(TIME * 0.55);
    float beatPhase = fract(TIME / 5.5);
    float beat = exp(-pow((beatPhase - 0.05) * 40.0, 2.0)) + 0.6 * exp(-pow((beatPhase - 0.13) * 40.0, 2.0));

    // Warped smoke and a few ridged claws reaching in.
    vec2 q = p * 2.4;
    vec2 warp = vec2(fbm(q + vec2(0.0, TIME * 0.06)), fbm(q + vec2(5.2, -TIME * 0.05)));
    float smoke = fbm(q * 1.4 + warp * 1.9 + vec2(TIME * 0.035, -TIME * 0.025));
    float ridge = 1.0 - abs(gnoise(vec2(angle * 2.6, TIME * 0.09)) * 2.0);
    float claws = pow(clamp(ridge, 0.0, 1.0), 7.0);
    float reach = 0.52 - claws * 0.16 - breath * 0.035 - beat * 0.025;
    float field = r + smoke * 0.16;
    float dark = smoothstep(reach - 0.04, reach + 0.3, field);

    // Smouldering violet rim where the darkness meets the light, brightest along the claws.
    float rim = exp(-pow((field - reach) * 16.0, 2.0)) * (0.35 + 0.65 * claws) * (0.75 + 0.25 * beat);

    // Wisps rising through the dark.
    float wisps = smoothstep(0.18, 0.5, fbm(vec2(p.x * 3.2, p.y * 2.2 + TIME * 0.22) + warp * 1.2)) * dark;

    vec3 shadow = vec3(0.03, 0.012, 0.05);
    vec3 color = mix(shadow, vec3(0.13, 0.05, 0.2), wisps * 0.6);
    color = mix(color, vec3(0.55, 0.22, 0.95), clamp(rim, 0.0, 1.0) * 0.7);
    float alpha = dark * 0.86 + rim * 0.3;

    // Eyes: in a few cells deep in the dark, a pair of violet eyes opens for a moment, blinks once, and closes.
    vec2 grid = vec2(9.0, 5.0);
    vec2 gp = UV * grid;
    vec2 cell = floor(gp);
    vec3 seed = hash32(cell + 3.7);
    float cycle = TIME / (6.0 + seed.y * 5.0) + seed.z * 10.0;
    vec3 h = hash32(cell + floor(cycle) * vec2(17.0, 31.0));
    float phase = fract(cycle);
    float open = smoothstep(0.0, 0.12, phase) * (1.0 - smoothstep(0.36, 0.5, phase));
    float blink = smoothstep(0.0, 0.012, abs(phase - 0.24));
    vec2 lp = (fract(gp) - 0.5 - (h.yz - 0.5) * 0.35) * vec2(aspect * grid.y / grid.x, 1.0);
    float eyeL = length((lp - vec2(-0.085, 0.0)) * vec2(1.0, 2.4));
    float eyeR = length((lp - vec2(0.085, 0.0)) * vec2(1.0, 2.4));
    float core = (1.0 - smoothstep(0.025, 0.05, eyeL)) + (1.0 - smoothstep(0.025, 0.05, eyeR));
    float halo = exp(-eyeL * 22.0) + exp(-eyeR * 22.0);
    float eyes = step(0.82, h.x) * open * blink * smoothstep(0.7, 0.95, dark);
    color = mix(color, vec3(0.78, 0.45, 1.0), clamp(core * eyes, 0.0, 1.0));
    color += vec3(0.35, 0.12, 0.55) * halo * eyes * 0.5;
    alpha = max(alpha, clamp(core * eyes, 0.0, 1.0));

    COLOR = vec4(color, clamp(alpha * intensity, 0.0, 0.92));
}
";

    private const string DustCloudsShader = @"shader_type canvas_item;

uniform float intensity = 1.0;

// Sine-free hash: stays precise at large coordinates, where sin-based hashes turn blocky.
vec3 hash32(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

vec2 gradient(vec2 i) {
    return hash32(i).xy * 2.0 - 1.0;
}

// Gradient noise with quintic fade: no flat grid cells, so thresholds never show square edges.
float gnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = dot(gradient(i), f);
    float b = dot(gradient(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
    float c = dot(gradient(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
    float d = dot(gradient(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Each octave is rotated and offset so no layer lines up with the axes.
float fbm(vec2 p) {
    mat2 rotation = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++) {
        value += amplitude * gnoise(p);
        p = rotation * p * 2.02 + vec2(1.7, 9.2);
        amplitude *= 0.5;
    }
    return value;
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 uv = vec2(UV.x * res.x / res.y, UV.y);
    vec2 drift = vec2(TIME * 0.03, -TIME * 0.006);
    // Domain warp: the clouds billow and swirl instead of sliding as a flat texture.
    vec2 warp = vec2(fbm(uv * 1.4 + drift), fbm(uv * 1.4 + vec2(5.2, 1.3) - drift));
    float n = fbm(uv * 1.8 + warp * 1.3 + drift * 1.5) + 0.5;
    float cloud = smoothstep(0.26, 0.76, n);
    float low = 0.7 + 0.3 * smoothstep(0.1, 1.0, UV.y);
    float alpha = cloud * 0.6 * low;
    vec3 dust = mix(vec3(0.4, 0.32, 0.23), vec3(0.66, 0.56, 0.43), clamp(warp.x * 1.5 + 0.5, 0.0, 1.0));

    vec2 p = UV * res + vec2(TIME * 18.0, sin(TIME * 0.3) * 10.0);
    vec2 cell = floor(p / 38.0);
    vec2 f = fract(p / 38.0);
    vec3 h = hash32(cell);
    if (h.z > 0.88) {
        vec2 center = h.xy * 0.6 + 0.2;
        float speck = smoothstep(0.07, 0.0, length(f - center)) * (0.5 + 0.5 * sin(TIME * 2.0 + h.z * 50.0));
        alpha += speck * 0.4;
        dust = mix(dust, vec3(0.78, 0.69, 0.55), speck);
    }

    COLOR = vec4(dust, clamp(alpha, 0.0, 1.0) * intensity);
}
";

    private const string BuzzingFliesShader = @"shader_type canvas_item;

uniform float intensity = 1.0;
uniform int fly_count = 12;

float hash(float n) {
    return fract(sin(n * 12.9898) * 43758.5453);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 p = UV * res;
    float scale = res.y / 1080.0;
    float alpha = 0.0;
    for (int i = 0; i < fly_count; i++) {
        float fi = float(i);
        // Each fly loops around its own spot on a wobbly figure-eight, with a fast jittery buzz on top.
        vec2 home = vec2(0.08 + 0.84 * hash(fi * 1.3 + 0.7), 0.1 + 0.8 * hash(fi * 2.9 + 4.1)) * res;
        float t = TIME * (0.55 + hash(fi * 5.1) * 0.5) + hash(fi * 7.7) * 50.0;
        float radius = (90.0 + hash(fi * 3.3) * 120.0) * scale;
        vec2 loop = vec2(sin(t * 1.3) + 0.6 * sin(t * 2.9 + 1.0), cos(t * 1.1) + 0.5 * sin(t * 3.7 + 2.0)) * radius;
        vec2 buzz = vec2(sin(TIME * 23.0 + fi * 4.0), cos(TIME * 29.0 + fi * 7.0)) * 3.0 * scale;
        vec2 pos = home + loop + buzz;
        vec2 d = p - pos;
        if (dot(d, d) > 400.0 * scale * scale) {
            continue;
        }
        vec2 velocity = vec2(1.3 * cos(t * 1.3) + 1.74 * cos(t * 2.9 + 1.0), -1.1 * sin(t * 1.1) + 1.85 * cos(t * 3.7 + 2.0));
        vec2 dir = normalize(velocity + vec2(0.0001));
        vec2 local = vec2(dot(d, dir), dot(d, vec2(-dir.y, dir.x))) / scale;
        float body = 1.0 - smoothstep(3.2, 4.4, length(local * vec2(0.7, 1.15)));
        float head = 1.0 - smoothstep(1.8, 2.6, length(local - vec2(4.2, 0.0)));
        float flap = 0.6 + 0.4 * sin(TIME * 70.0 + fi * 3.0);
        vec2 wing_scale = vec2(1.0, 1.7);
        float wing_left = 1.0 - smoothstep(2.6, 3.8, length((local - vec2(-1.5, 3.4 * flap)) * wing_scale));
        float wing_right = 1.0 - smoothstep(2.6, 3.8, length((local - vec2(-1.5, -3.4 * flap)) * wing_scale));
        alpha = max(alpha, max(body, head) * 0.9);
        alpha = max(alpha, max(wing_left, wing_right) * 0.3);
    }
    COLOR = vec4(vec3(0.05, 0.06, 0.03), alpha * intensity);
}
";

    private const string GhostlyFormsShader = @"shader_type canvas_item;

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

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 uv = vec2(UV.x * aspect, UV.y);
    float forms = 0.0;
    for (int i = 0; i < 6; i++) {
        float fi = float(i);
        // Each form drifts slowly to the right across the screen, bobbing, then comes back around from the left.
        float speed = 0.02 + 0.018 * fract(fi * 0.618);
        float x = mod(fi * 0.53 * aspect + TIME * speed, aspect + 1.0) - 0.5;
        float y = 0.18 + 0.64 * fract(fi * 0.731 + 0.2) + 0.05 * sin(TIME * 0.35 + fi * 2.0);
        vec2 d = uv - vec2(x, y);
        float radius = 0.13 + 0.05 * fract(fi * 0.43);
        float wobble = fbm(d * 3.2 + vec2(fi * 7.1, TIME * 0.18)) * 0.1;
        float body = 1.0 - smoothstep(radius * 0.3, radius, length(d * vec2(1.0, 0.8)) + wobble);
        // A wavering wisp trails behind (to the left), thinning out.
        float trail = smoothstep(-0.6, -0.04, d.x) * step(d.x, 0.0);
        float wisp_y = d.y + 0.025 * sin(d.x * 18.0 - TIME * 1.6 + fi);
        float tail = (1.0 - smoothstep(0.0, radius * 0.75 * trail + 0.001, abs(wisp_y) + wobble * 0.5)) * trail;
        forms = max(forms, max(body, tail * 0.75));
    }
    float mist = smoothstep(0.4, 0.95, fbm(uv * 1.8 + vec2(TIME * 0.015, -TIME * 0.01)) + 0.5);
    float low = 0.6 + 0.4 * smoothstep(0.2, 1.0, UV.y);
    float alpha = clamp(forms * 0.3 + mist * 0.12 * low, 0.0, 1.0);
    vec3 green = mix(vec3(0.3, 0.8, 0.58), vec3(0.78, 1.0, 0.9), forms);
    COLOR = vec4(green, alpha * intensity);
}
";

    private const string SandstormShader = @"shader_type canvas_item;

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

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = res.x / res.y;
    vec2 uv = vec2(UV.x * aspect, UV.y);

    // Gusting haze: noise stretched sideways and blown left to right, pulsing in gusts.
    vec2 wind = vec2(-TIME * 0.2, TIME * 0.012);
    float haze = fbm(vec2(uv.x * 1.2, uv.y * 5.0) + wind) + 0.5;
    float haze2 = fbm(vec2(uv.x * 2.5, uv.y * 9.0) + wind * 1.6 + vec2(3.1, 7.3)) + 0.5;
    float gust = 0.7 + 0.3 * sin(TIME * 0.4 + uv.y * 3.0);
    float sand = smoothstep(0.48, 0.95, haze * 0.7 + haze2 * 0.4) * gust;

    // Streaks of blowing grains in three speeds.
    float streaks = 0.0;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        vec2 p = UV * res;
        p.x -= TIME * (420.0 + fi * 160.0);
        p.y += sin(p.x * 0.004 + fi) * 6.0;
        vec2 cell_size = vec2(90.0, 14.0);
        vec2 cell = floor(p / cell_size);
        vec2 f = fract(p / cell_size);
        vec3 h = hash32(cell + fi * 13.0);
        if (h.z > 0.9) {
            float len = 0.3 + 0.5 * h.x;
            float x = f.x - h.y * (1.0 - len);
            float s = smoothstep(0.0, 0.08, x) * (1.0 - smoothstep(len * 0.6, len, x)) * (1.0 - smoothstep(0.05, 0.18, abs(f.y - 0.5)));
            streaks = max(streaks, s * (0.5 - fi * 0.12));
        }
    }

    vec3 color = mix(vec3(0.86, 0.72, 0.5), vec3(0.96, 0.86, 0.66), streaks);
    float alpha = clamp(sand * 0.17 + streaks * 0.32, 0.0, 0.5);

    // Now and then a swarm of metallic sand shards tumbles through on the wind. Each swarm crosses the whole screen from off
    // the left edge to off the right edge, so it never pops in or out. Every shard is its own soft, motion-stretched sliver
    // that swirls around the swarm centre, with a steel body, a bright specular glint that flashes as it turns, and a faint
    // glow, so nothing is clipped to a grid.
    for (int s = 0; s < 2; s++) {
        float fs = float(s);
        float t = TIME / (7.5 + fs * 3.1) + fs * 0.41;
        float window = floor(t);
        float phase = fract(t);
        vec3 h = hash32(vec2(window, fs * 5.0 + 1.0));
        if (h.z > 0.6) {
            continue;
        }
        float radius = 0.09 + 0.04 * h.x;
        vec2 center = vec2(mix(-0.45, aspect + 0.45, phase), 0.22 + 0.56 * h.y + 0.06 * sin(phase * 9.0 + h.x * 6.0));
        vec2 rel = uv - center;
        if (dot(rel, rel) > (radius + 0.05) * (radius + 0.05)) {
            continue;
        }
        vec3 shard_color = vec3(0.0);
        float shard_alpha = 0.0;
        for (int g = 0; g < 18; g++) {
            float fg = float(g);
            vec3 gh = hash32(vec2(fg * 3.7 + window, fs * 11.0 + 2.0));
            float orbit = gh.x * 6.2831 + TIME * (0.8 + gh.y * 1.4) * (gh.z > 0.5 ? 1.0 : -1.0);
            float dist = radius * sqrt(gh.y) * (0.85 + 0.15 * sin(TIME * 2.0 + fg));
            vec2 pos = vec2(cos(orbit), sin(orbit) * 0.6) * dist;
            vec2 d = rel - pos;
            float spin = TIME * (3.0 + 4.0 * gh.z) + gh.x * 20.0;
            vec2 axis = vec2(cos(spin * 0.15 + gh.y), sin(spin * 0.15 + gh.y));
            // Stretched along the wind (motion blur) and along its own long axis.
            vec2 local = vec2(dot(d, axis), dot(d, vec2(-axis.y, axis.x)));
            float size = 0.0045 + 0.004 * gh.z;
            float sliver = length(vec2(local.x / (size * 2.6), local.y / size));
            float blur = length(vec2((d.x + 0.012) / 0.022, d.y / size));
            float core = 1.0 - smoothstep(0.55, 1.0, sliver);
            float trail = (1.0 - smoothstep(0.3, 1.0, blur)) * 0.35;
            float glint = pow(max(0.0, sin(spin)), 12.0);
            float edge_light = clamp(0.5 + local.y / size * 0.5, 0.0, 1.0);
            vec3 steel = mix(vec3(0.28, 0.3, 0.34), vec3(0.78, 0.82, 0.88), edge_light);
            vec3 c = steel + vec3(1.0, 0.98, 0.92) * glint * 1.4;
            float a = max(core, trail);
            shard_color = mix(shard_color, c, a);
            shard_alpha = max(shard_alpha, a * (0.85 + 0.15 * glint));
            // Soft metallic glow around bright glints.
            float halo = (1.0 - smoothstep(0.0, size * 5.0, length(d))) * glint;
            shard_color += vec3(0.7, 0.8, 0.95) * halo * 0.25;
            shard_alpha = max(shard_alpha, halo * 0.35);
        }
        // Faint dark metallic haze inside the swarm ties the shards together.
        float haze = (1.0 - smoothstep(radius * 0.2, radius + 0.04, length(rel * vec2(1.0, 1.6)))) * 0.1;
        color = mix(color, vec3(0.35, 0.36, 0.4), haze * (1.0 - shard_alpha));
        alpha = max(alpha, haze);
        color = mix(color, shard_color, shard_alpha);
        alpha = max(alpha, shard_alpha * 0.92);
    }

    COLOR = vec4(color, alpha * intensity);
}
";

    private static readonly Dictionary<ZoneScreenEffect, Shader> Shaders = new();

    /// <summary>Called on every peer after entering a room; adds the zone's screen effect to that room's node, if any.</summary>
    public static void OnRoomEntered(IRunState runState, AbstractRoom room)
    {
        try
        {
            ZoneScreenEffect effect = ScreenEffectRules.For(ZoneContext.Current(runState)?.Biome.Id);
            if (effect != ZoneScreenEffect.None)
            {
                Attach(room, effect, 0);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to prepare a zone screen effect: {ex}");
        }
    }

    /// <summary>The room's node may be created after the room is entered, so look for it again over the next frames.</summary>
    private static void Attach(AbstractRoom room, ZoneScreenEffect effect, int attempt)
    {
        try
        {
            Control? host = HostFor(room);
            if (host == null || !GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            {
                if (attempt < MaxRetries)
                {
                    Callable.From(() => Attach(room, effect, attempt + 1)).CallDeferred();
                }
                else
                {
                    Log.Warn($"Zone screen effect {effect}: no room node found for {room.GetType().Name}.");
                }

                return;
            }

            if (host.GetNodeOrNull(NodeName) != null)
            {
                return;
            }

            var overlay = new ColorRect
            {
                Name = NodeName,
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = ShaderFor(effect) },
            };
            host.AddChild(overlay);
            overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            if (host is NCombatRoom combatRoom && combatRoom.Ui is { } ui && ui.GetParent() == host)
            {
                host.MoveChild(overlay, ui.GetIndex());
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the zone screen effect {effect}: {ex}");
        }
    }

    private static Control? HostFor(AbstractRoom room) => room switch
    {
        CombatRoom => NRun.Instance?.CombatRoom,
        MerchantRoom => NRun.Instance?.MerchantRoom,
        RestSiteRoom => NRun.Instance?.RestSiteRoom,
        EventRoom => NRun.Instance?.EventRoom,
        TreasureRoom => NRun.Instance?.TreasureRoom,
        _ => null,
    };

    private static Shader ShaderFor(ZoneScreenEffect effect)
    {
        if (!Shaders.TryGetValue(effect, out Shader? shader))
        {
            shader = new Shader
            {
                Code = effect switch
                {
                    ZoneScreenEffect.BloodRain => BloodRainShader,
                    ZoneScreenEffect.GoldenRays => GoldenRaysShader,
                    ZoneScreenEffect.MirrorShine => MirrorShineShader,
                    ZoneScreenEffect.DustClouds => DustCloudsShader,
                    ZoneScreenEffect.BuzzingFlies => BuzzingFliesShader,
                    ZoneScreenEffect.GhostlyForms => GhostlyFormsShader,
                    ZoneScreenEffect.Sandstorm => SandstormShader,
                    ZoneScreenEffect.CrumblingRuins => CrumblingRuinsShader,
                    ZoneScreenEffect.CreepingShadow => CreepingShadowShader,
                    ZoneScreenEffect.GoldenMandalas => GoldenMandalasShader,
                    ZoneScreenEffect.DriftingFrost => DriftingFrostShader,
                    ZoneScreenEffect.BlindingRadiance => BlindingRadianceShader,
                    ZoneScreenEffect.FermentingGas => FermentingGasShader,
                    _ => PrismaticLightShader,
                },
            };
            Shaders[effect] = shader;
        }

        return shader;
    }
}
