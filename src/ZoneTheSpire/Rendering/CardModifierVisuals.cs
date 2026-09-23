using System;
using System.Reflection;
using BaseLib.Extensions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ZoneTheSpire.Run.Ferrosand;
using ZoneTheSpire.Run.HallsOfMidas;
using ZoneTheSpire.Run.Phantasmal;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Subtle overlays on cards with zone card modifiers, everywhere a card is drawn (hand, rewards, deck, shop): faint rising
/// ghostly wisps and a soft green edge glow on Phantasm-Haunted cards, a dirty green patchy grime on Wriggling cards, a steel
/// sheen with flickering electric arcs on Magnetic cards, a gold rim and sweeping glint on Gilded cards, a calm saffron lotus glow on Chakra cards, cold blue frost crystals on Biting Cold cards, a sheet of ice over Frozen cards, dark violet creeping
/// in from the edges with drifting black veins on Shadow Corrupted cards, an ornate blinking divine eye in the top-right corner and golden veins rising
/// from the bottom edge on Hallowing cards, soft white holy light threaded with dark red veins on Blasphemous cards, a pulsing
/// crimson border on top of that on this turn's Blasphemous marks, and soft pearl light with white feathers drifting down on
/// Redemption cards. Each is
/// a ColorRect with a shader laid over the card frame (same parent, position and size), kept in sync on every card refresh.
/// Ignores the mouse. Local rendering only; never affects gameplay.
/// </summary>
internal static partial class CardModifierVisuals
{
    private const string GhostName = "ZoneTheSpireHauntedCardFx";
    private const string GrimeName = "ZoneTheSpireWrigglingCardFx";
    private const string MagneticName = "ZoneTheSpireMagneticCardFx";
    private const string GildedName = "ZoneTheSpireGildedCardFx";
    private const string ChakraName = "ZoneTheSpireChakraCardFx";
    private const string BitingColdName = "ZoneTheSpireBitingColdCardFx";
    private const string FrozenName = "ZoneTheSpireFrozenCardFx";
    private const string ShadowCorruptedName = "ZoneTheSpireShadowCorruptedCardFx";
    private const string HallowingName = "ZoneTheSpireHallowingCardFx";
    private const string BlasphemousName = "ZoneTheSpireBlasphemousCardFx";
    private const string BlasphemousMarkName = "ZoneTheSpireBlasphemousMarkCardFx";
    private const string RedemptionName = "ZoneTheSpireRedemptionCardFx";

    private const string SharedFunctions = @"
uniform vec2 rect_size = vec2(300.0, 422.0);

vec3 hash32(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

// 1 inside the card's rounded rectangle, fading out over its last few pixels.
float card_mask(vec2 uv) {
    vec2 p = (uv - 0.5) * rect_size;
    float radius = 22.0;
    vec2 q = abs(p) - (rect_size * 0.5 - vec2(radius + 4.0));
    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    return 1.0 - smoothstep(-6.0, 0.0, d);
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
    for (int i = 0; i < 3; i++) {
        value += amplitude * gnoise(p);
        p = rotation * p * 2.02 + vec2(1.7, 9.2);
        amplitude *= 0.5;
    }
    return value;
}
";

    private const string GhostShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
void fragment() {
    vec2 p = UV * rect_size / 110.0;
    // Two layers of wisps rise and waver, drifting at different speeds.
    float wisp = fbm(p + vec2(0.15 * sin(TIME * 0.4), TIME * 0.22));
    float wisp2 = fbm(p * 1.7 + vec2(-0.2 * sin(TIME * 0.3), TIME * 0.38) + vec2(4.3, 1.1));
    float wisps = smoothstep(0.02, 0.38, wisp + 0.12 * sin(TIME * 0.7 + UV.y * 6.0))
        + 0.6 * smoothstep(0.08, 0.42, wisp2);
    float pulse = 0.75 + 0.25 * sin(TIME * 1.4);
    float edge = smoothstep(0.32, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5)));
    float haze = 0.05 + 0.03 * sin(TIME * 0.9 + UV.y * 3.0);
    float glow = wisps * 0.2 + edge * 0.26 * pulse + haze;
    COLOR = vec4(vec3(0.45, 1.0, 0.8) * glow * card_mask(UV), 1.0);
}
";

    // Static green grime: large blotchy stains, finer mottling and specks, heavier towards the edges. Fixed per card
    // position (no animation), so it reads as dirt on the card rather than an effect.
    private const string GrimeShader = @"shader_type canvas_item;
" + SharedFunctions + @"
void fragment() {
    vec2 p = UV * rect_size / 90.0;
    float stains = fbm(p + vec2(3.7, 1.2)) + 0.5;
    float mottle = fbm(p * 2.6 + vec2(8.1, 4.4)) + 0.5;
    float specks = smoothstep(0.74, 0.88, fbm(p * 7.0 + vec2(1.3, 7.7)) + 0.5);
    float patches = smoothstep(0.5, 0.78, stains * 0.7 + mottle * 0.35);
    float edge = smoothstep(0.28, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5)));
    float grime = clamp(patches * 0.32 + specks * 0.22 + edge * (0.1 + 0.18 * mottle), 0.0, 0.55);
    vec3 dirt = mix(vec3(0.14, 0.24, 0.06), vec3(0.36, 0.42, 0.14), clamp(mottle, 0.0, 1.0));

    // A few small flies buzz over the card: each loops a wobbly figure-eight around its own spot with a fast jitter,
    // facing where it flies, with flickering translucent wings.
    vec2 pixel = UV * rect_size;
    float body_alpha = 0.0;
    float wing_alpha = 0.0;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        vec3 h = hash32(vec2(fi * 7.3 + 1.0, 2.1));
        vec2 home = vec2(0.22 + 0.56 * h.x, 0.2 + 0.6 * h.y) * rect_size;
        float t = TIME * (0.9 + 0.5 * h.z) + h.x * 40.0;
        float radius = 28.0 + 26.0 * h.z;
        vec2 loop_offset = vec2(sin(t * 1.3) + 0.6 * sin(t * 2.9 + 1.0), cos(t * 1.1) + 0.5 * sin(t * 3.7 + 2.0)) * radius;
        vec2 buzz = vec2(sin(TIME * 23.0 + fi * 4.0), cos(TIME * 29.0 + fi * 7.0)) * 1.5;
        vec2 d = pixel - (home + loop_offset + buzz);
        if (dot(d, d) > 144.0) {
            continue;
        }
        vec2 velocity = vec2(1.3 * cos(t * 1.3) + 1.74 * cos(t * 2.9 + 1.0), -1.1 * sin(t * 1.1) + 1.85 * cos(t * 3.7 + 2.0));
        vec2 dir = normalize(velocity + vec2(0.0001));
        vec2 local = vec2(dot(d, dir), dot(d, vec2(-dir.y, dir.x)));
        float body = 1.0 - smoothstep(2.4, 3.3, length(local * vec2(0.7, 1.15)));
        float head = 1.0 - smoothstep(1.3, 2.0, length(local - vec2(3.2, 0.0)));
        float flap = 0.6 + 0.4 * sin(TIME * 70.0 + fi * 3.0);
        float wing_left = 1.0 - smoothstep(2.0, 3.0, length((local - vec2(-1.2, 2.7 * flap)) * vec2(1.0, 1.7)));
        float wing_right = 1.0 - smoothstep(2.0, 3.0, length((local - vec2(-1.2, -2.7 * flap)) * vec2(1.0, 1.7)));
        body_alpha = max(body_alpha, max(body, head));
        wing_alpha = max(wing_alpha, max(wing_left, wing_right));
    }

    vec3 color = mix(dirt, vec3(0.78, 0.84, 0.78), wing_alpha);
    float alpha = max(grime, wing_alpha * 0.35);
    color = mix(color, vec3(0.05, 0.06, 0.03), body_alpha);
    alpha = max(alpha, body_alpha * 0.92);
    COLOR = vec4(color, alpha * card_mask(UV));
}
";

    // Magnetic: the card sits in a magnetic field. Curved field lines run between a pole at the top and one at the bottom,
    // drawn as streams of tiny iron filings that flow along them; the poles glow with a slow pulse, and now and then an
    // electric charge races along one field line with a jittering, flickering arc. Additive and kept soft over the text.
    private const string MagneticShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
// TAU is a built-in shader constant in Godot 4; redefining it fails compilation (the overlay then draws plain white).
const float LINES = 14.0;

void fragment() {
    float aspect = rect_size.x / rect_size.y;
    vec2 q = vec2(UV.x * aspect, UV.y);
    vec2 north = vec2(0.5 * aspect, 0.1);
    vec2 south = vec2(0.5 * aspect, 0.9);
    vec2 a = q - north;
    vec2 b = q - south;
    float ra = max(length(a), 0.0001);
    float rb = max(length(b), 0.0001);

    // Dipole field lines are the level curves of the angle difference; the potential runs along them.
    float line_coord = (atan(a.y, a.x) - atan(b.y, b.x)) / TAU * LINES;
    float along = log(ra / rb);
    float line_id = floor(line_coord);
    float to_line = abs(fract(line_coord) - 0.5) * 2.0;
    float width = fwidth(line_coord) * 1.6 + 0.02;
    float line = 1.0 - smoothstep(0.0, width * 2.0, 1.0 - to_line);

    // Filings: short dashes flowing north to south along each line, each line at its own speed and offset.
    vec3 lh = hash32(vec2(line_id, 7.0));
    float flow = fract(along * 2.2 - TIME * (0.35 + 0.3 * lh.x) + lh.y);
    float dash = smoothstep(0.0, 0.12, flow) * (1.0 - smoothstep(0.35, 0.55, flow));
    float pole_fade = smoothstep(0.03, 0.12, min(ra, rb));
    float filings = line * dash * pole_fade;
    float faint_line = line * pole_fade * 0.25;

    // Pole glows.
    float pulse = 0.7 + 0.3 * sin(TIME * 1.8);
    float poles = (exp(-ra * 16.0) + exp(-rb * 16.0)) * pulse;

    // Electric charge: every ~0.8 s a random line crackles for a moment, the spark travelling along it.
    float slot = floor(TIME * 1.25);
    float slot_phase = fract(TIME * 1.25);
    vec3 sh = hash32(vec2(slot, 3.0));
    float arc_line = floor(sh.x * LINES);
    float on_arc_line = 1.0 - step(0.5, abs(line_id - arc_line));
    float flicker = step(0.35, hash32(vec2(floor(TIME * 24.0), slot)).y);
    float jitter = (hash32(vec2(floor(along * 9.0), floor(TIME * 24.0))).z - 0.5) * 0.35;
    float arc_width = fwidth(line_coord) * 1.2 + 0.015;
    float to_arc = abs(fract(line_coord + jitter * 0.08) - 0.5) * 2.0;
    float arc_core = smoothstep(1.0 - arc_width * 2.0, 1.0, to_arc);
    float spark_pos = mix(-2.2, 2.2, slot_phase / 0.35);
    float spark = exp(-abs(along - spark_pos) * 2.5) * step(slot_phase, 0.35);
    float arc = on_arc_line * arc_core * spark * flicker * pole_fade;

    float edge = 1.0 - smoothstep(0.0, 0.08, min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y)));
    vec3 steel = vec3(0.72, 0.78, 0.88);
    vec3 electric = vec3(0.55, 0.82, 1.0);
    vec3 color = steel * (filings * 0.32 + faint_line * 0.12 + edge * 0.05)
        + electric * (poles * 0.28 + arc * 0.9);
    COLOR = vec4(color * card_mask(UV), 1.0);
}
";

    // Chakra: a calm saffron glow breathing out from the middle of the card, with a slowly turning ring of lotus petals behind
    // it. Additive and faint, so the card's art and text stay readable.
    private const string ChakraShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
void fragment() {
    vec2 p = (UV - vec2(0.5, 0.42)) * vec2(rect_size.x / rect_size.y, 1.0);
    float r = length(p);
    float a = atan(p.y, p.x) + TIME * 0.12;
    float breath = 0.72 + 0.28 * sin(TIME * 0.9);

    // A ring of eight lotus petals, turning slowly.
    float petals = cos(a * 8.0) * 0.5 + 0.5;
    float ring = exp(-pow((r - 0.2 - petals * 0.05) * 11.0, 2.0)) * 0.5;
    float bloom = exp(-pow(r * 3.4, 2.0)) * 0.35;
    float edge = smoothstep(0.36, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5))) * 0.14;

    vec3 saffron = vec3(1.0, 0.72, 0.3);
    COLOR = vec4(saffron * (ring + bloom + edge) * breath * card_mask(UV), 1.0);
}
";

    // Gilded: a warm gold rim and a soft gold wash, with a bright diagonal glint sweeping across the card every few seconds
    // and a few tiny sparkles twinkling near the edges. Additive and kept faint over the text.
    private const string GildedShader = @"shader_type canvas_item;
render_mode blend_add;
" + SharedFunctions + @"
void fragment() {
    vec3 gold = vec3(1.0, 0.78, 0.32);
    float edge = smoothstep(0.34, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5)));
    float wash = 0.04 + 0.02 * sin(TIME * 0.8 + UV.x * 4.0);
    float sweep = fract(TIME * 0.22) * 2.6 - 0.8;
    float glint = exp(-pow((UV.x + UV.y * 0.6 - sweep) * 9.0, 2.0));
    vec2 cell = floor(UV * rect_size / 26.0);
    vec3 h = hash32(cell);
    vec2 local = fract(UV * rect_size / 26.0) - 0.5;
    float twinkle = pow(max(0.0, sin(TIME * (1.5 + h.y * 2.0) + h.z * 40.0)), 12.0);
    float sparkle = step(0.9, h.x) * (1.0 - smoothstep(0.02, 0.12, length(local))) * twinkle * (0.3 + edge);
    float glow = edge * 0.22 + wash + glint * 0.16 + sparkle * 0.8;
    COLOR = vec4(gold * glow * card_mask(UV), 1.0);
}
";

    // Shadow Corrupted: dark violet creeping in from the edges, with black veins slowly drifting through it and a faint violet
    // pulse along the rim. Normal blend (it darkens), kept light over the middle of the card so the text stays readable.
    private const string ShadowCorruptedShader = @"shader_type canvas_item;
" + SharedFunctions + @"
void fragment() {
    vec2 centered = UV - 0.5;
    float edge = smoothstep(0.22, 0.5, max(abs(centered.x), abs(centered.y)));
    vec2 p = UV * rect_size / 70.0;
    float warp = fbm(p + vec2(TIME * 0.05, -TIME * 0.07));
    float veins = 1.0 - smoothstep(0.0, 0.07, abs(fbm(p * 1.6 + warp * 1.4 + vec2(0.0, TIME * 0.04))));
    float creep = clamp(edge + warp * 0.35, 0.0, 1.0);
    float pulse = 0.5 + 0.5 * sin(TIME * 1.3);
    vec3 violet = vec3(0.30, 0.12, 0.42);
    vec3 color = mix(violet, vec3(0.03, 0.0, 0.06), veins);
    float alpha = creep * 0.55 + veins * (0.12 + edge * 0.45) + edge * pulse * 0.08;
    COLOR = vec4(color, clamp(alpha, 0.0, 0.8) * card_mask(UV));
}
";

    private static readonly FieldInfo? FrameField = AccessTools.Field(typeof(NCard), "_frame");
    private static readonly FieldInfo? EnergyLabelField = AccessTools.Field(typeof(NCard), "_energyLabel");
    private static readonly Color GhostCostColor = new(0.72f, 1f, 0.86f);
    private static readonly Color GhostCostOutline = new(0.08f, 0.3f, 0.22f);
    private static ShaderMaterial? _ghostMaterial;
    private static ShaderMaterial? _grimeMaterial;
    private static ShaderMaterial? _magneticMaterial;
    private static ShaderMaterial? _gildedMaterial;
    private static ShaderMaterial? _chakraMaterial;
    private static ShaderMaterial? _bitingColdMaterial;
    private static ShaderMaterial? _frozenMaterial;
    private static ShaderMaterial? _shadowCorruptedMaterial;
    private static bool _warned;

    public static void Refresh(NCard card)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(card)
                || FrameField?.GetValue(card) is not TextureRect frame
                || !GodotObject.IsInstanceValid(frame)
                || frame.GetParent() is not Control parent)
            {
                return;
            }

            // A shaded card (Shadow Corruption) hides its face, so none of the modifier looks below may give it away.
            bool shaded = SyncShade(card, parent, frame);
            CardModel? model = shaded ? null : card.Model;
            bool haunted = model != null && (model.TryGetModifier<PhantasmHauntedModifier>(out _)
                || model.TryGetModifier<FlutteringPhantasmModifier>(out _));
            // Both are drawn over the whole card (art and text too).
            Sync(parent, frame, GhostName, haunted, () => _ghostMaterial ??= Material(GhostShader, frame.Size), onTop: true);
            Sync(parent, frame, GrimeName, model != null && model.TryGetModifier<WrigglingModifier>(out _), () => _grimeMaterial ??= Material(GrimeShader, frame.Size), onTop: true);
            Sync(parent, frame, MagneticName, model != null && model.TryGetModifier<MagneticModifier>(out _), () => _magneticMaterial ??= Material(MagneticShader, frame.Size), onTop: true);
            Sync(parent, frame, GildedName, model != null && model.TryGetModifier<GildedModifier>(out _), () => _gildedMaterial ??= Material(GildedShader, frame.Size), onTop: true);
            Sync(parent, frame, ChakraName, Run.Devas.ChakraModifier.AmountOf(model) > 0, () => _chakraMaterial ??= Material(ChakraShader, frame.Size), onTop: true);
            Sync(parent, frame, BitingColdName, Run.Hoarfrost.BitingColdModifier.AmountOf(model) > 0, () => _bitingColdMaterial ??= Material(BitingColdShader, frame.Size), onTop: true);
            Sync(parent, frame, FrozenName, Run.Hoarfrost.FrozenCards.IsFrozen(model), () => _frozenMaterial ??= Material(FrozenShader, frame.Size), onTop: true);
            Sync(parent, frame, ShadowCorruptedName, Run.Shadow.ShadowCorruptedModifier.IsCorrupted(model), () => _shadowCorruptedMaterial ??= Material(ShadowCorruptedShader, frame.Size), onTop: true);
            Sync(parent, frame, HallowingName, Run.Hallowed.HallowingModifier.Has(model), () => _hallowingMaterial ??= Material(HallowingShader, frame.Size), onTop: true);
            Sync(parent, frame, BlasphemousName, Run.Hallowed.BlasphemousModifier.Has(model), () => _blasphemousMaterial ??= Material(BlasphemousShader, frame.Size), onTop: true);
            Sync(parent, frame, RedemptionName, Run.Hallowed.RedemptionModifier.Has(model), () => _redemptionMaterial ??= Material(RedemptionShader, frame.Size), onTop: true);
            // A permanently Blasphemous card shows only its permanent look, even when marked (the mark adds nothing to it).
            Sync(parent, frame, BlasphemousMarkName, Run.Hallowed.BlasphemousMark.IsMarked(model) && !Run.Hallowed.BlasphemousModifier.Has(model), () => _blasphemousMarkMaterial ??= Material(BlasphemousMarkShader, frame.Size), onTop: true);
            SyncMarble(parent, frame, model);
            if (haunted && model != null && !model.EnergyCost.CostsX)
            {
                TintCost(card);
            }
        }
        catch (Exception ex)
        {
            if (!_warned)
            {
                _warned = true;
                Log.Warn($"Failed to update a card's zone modifier visuals: {ex}");
            }
        }
    }

    /// <summary>
    /// Haunted cards show their (lowered) cost in light ghostly green. Only the default cream colour is replaced, so the game's
    /// own signals (red when unaffordable, green right after an upgrade) still show.
    /// </summary>
    private static void TintCost(NCard card)
    {
        if (EnergyLabelField?.GetValue(card) is not Label label || !GodotObject.IsInstanceValid(label))
        {
            return;
        }

        Color current = label.GetThemeColor(ThemeConstants.Label.FontColor);
        if (current.IsEqualApprox(StsColors.cream) || current.IsEqualApprox(GhostCostColor))
        {
            label.AddThemeColorOverride(ThemeConstants.Label.FontColor, GhostCostColor);
            label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, GhostCostOutline);
        }
    }

    private static void Sync(Control parent, TextureRect frame, string name, bool wanted, Func<ShaderMaterial> material, bool onTop)
    {
        ColorRect? overlay = parent.GetNodeOrNull<ColorRect>(name);
        if (!wanted)
        {
            if (overlay != null)
            {
                parent.RemoveChild(overlay);
                overlay.QueueFree();
            }

            return;
        }

        if (overlay == null)
        {
            overlay = new ColorRect
            {
                Name = name,
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = material(),
            };
            parent.AddChild(overlay);
            if (!onTop)
            {
                parent.MoveChild(overlay, frame.GetIndex() + 1);
            }
        }

        if (onTop && overlay.GetIndex() != parent.GetChildCount() - 1)
        {
            parent.MoveChild(overlay, parent.GetChildCount() - 1);
        }

        overlay.Position = frame.Position;
        overlay.Size = frame.Size;
        overlay.Scale = frame.Scale;
        overlay.Rotation = frame.Rotation;
        overlay.PivotOffset = frame.PivotOffset;
    }

    private static ShaderMaterial Material(string code, Vector2 size)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = code } };
        if (size.X > 1f && size.Y > 1f)
        {
            material.SetShaderParameter("rect_size", size);
        }

        return material;
    }
}
