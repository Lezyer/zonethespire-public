using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Shaded cards (Shadow Corruption): the card's title, text and art are hidden and a dark drifting smoke covers the frame, so
/// only its cost and card type remain readable. Everything hidden here is restored as soon as the card is no longer shaded.
/// Local rendering only.
/// </summary>
internal static partial class CardModifierVisuals
{
    private const string ShadeName = "ZoneTheSpireShadedCardFx";
    private const string ShadeHiddenMeta = "zts_shade_hidden";

    private static readonly string[] ShadeHiddenFields = { "_titleLabel", "_banner", "_descriptionLabel", "_portrait", "_portraitBorder", "_ancientBorder" };

    private static ShaderMaterial? _shadeMaterial;

    // Dark violet smoke drifting slowly over the card, a little lighter at the edges. Opaque enough to hide the frame art.
    private const string ShadeShader = @"shader_type canvas_item;
" + SharedFunctions + @"
void fragment() {
    vec2 p = UV * rect_size / 120.0;
    float n = fbm(p + vec2(TIME * 0.05, -TIME * 0.08));
    float n2 = fbm(p * 1.8 - vec2(TIME * 0.07, TIME * 0.03) + vec2(3.1, 1.7));
    float smoke = clamp(0.5 + n * 0.8 + n2 * 0.4, 0.0, 1.0);
    vec3 color = mix(vec3(0.03, 0.02, 0.05), vec3(0.16, 0.09, 0.24), smoke);
    float edge = smoothstep(0.30, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5)));
    color += vec3(0.25, 0.12, 0.40) * edge * 0.35;
    COLOR = vec4(color, 0.92 * card_mask(UV));
}
";

    /// <summary>Shows or clears the shade on <paramref name="card"/>; returns whether the card is shaded.</summary>
    private static bool SyncShade(NCard card, Control parent, TextureRect frame)
    {
        bool shaded = ShadedCards.IsShaded(card.Model);
        foreach (string fieldName in ShadeHiddenFields)
        {
            if (AccessTools.Field(typeof(NCard), fieldName)?.GetValue(card) is not CanvasItem item || !GodotObject.IsInstanceValid(item))
            {
                continue;
            }

            if (shaded && item.Visible)
            {
                item.Visible = false;
                item.SetMeta(ShadeHiddenMeta, true);
            }
            else if (!shaded && item.HasMeta(ShadeHiddenMeta))
            {
                item.Visible = true;
                item.RemoveMeta(ShadeHiddenMeta);
            }
        }

        // Drawn just above the frame, so the cost and the type plaque (drawn after it) stay readable.
        Sync(parent, frame, ShadeName, shaded, () => _shadeMaterial ??= Material(ShadeShader, frame.Size), onTop: false);
        return shaded;
    }
}
