using Godot;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Run.ForgottenEmpire;

namespace ZoneTheSpire.Rendering;

internal static partial class CardModifierVisuals
{
    private const string MarbleName = "ZoneTheSpireMarbleCardFx";

    // Marbled cards: pale veined marble creeping over the card from its edges, with dark branching veins (lighter in the middle
    // so the text stays readable) and a slow white sheen. Marbling cards get the same stone, fainter and only near the edges.
    private const string MarbleShader = @"shader_type canvas_item;
uniform float strength = 1.0;
" + SharedFunctions + @"
void fragment() {
    vec2 p = UV * rect_size / 120.0;
    float n = fbm(p + vec2(3.1, 7.4));
    float n2 = fbm(p * 2.3 + vec2(n * 1.5, 1.7));
    float vein = 1.0 - smoothstep(0.0, 0.03, abs(n + 0.5 * n2));
    float fine = 1.0 - smoothstep(0.0, 0.02, abs(n2 - 0.1));
    float edge = smoothstep(0.3, 0.5, max(abs(UV.x - 0.5), abs(UV.y - 0.5)));
    float reach = mix(0.3, 1.0, edge);
    float veins = (vein * 0.6 + fine * 0.3) * reach * strength;
    float band = fract((UV.x + UV.y * 0.6) * 0.8 - TIME * 0.1);
    float sheen = smoothstep(0.0, 0.06, band) * (1.0 - smoothstep(0.06, 0.16, band));
    vec3 color = mix(vec3(0.95, 0.94, 0.91), vec3(0.36, 0.37, 0.4), clamp(veins * 1.6, 0.0, 1.0));
    color = mix(color, vec3(1.0), sheen * 0.6);
    float alpha = (edge * 0.3 + 0.05) * strength;
    alpha = max(alpha, veins * 0.55) + sheen * 0.16 * strength;
    COLOR = vec4(color, clamp(alpha, 0.0, 0.8) * card_mask(UV));
}
";

    private const string MarblingName = "ZoneTheSpireMarblingCardFx";

    private static ShaderMaterial? _marbleMaterial;
    private static ShaderMaterial? _marblingMaterial;

    private static void SyncMarble(Control parent, TextureRect frame, CardModel? model)
    {
        bool marbled = MarbleCards.IsMarbled(model);
        bool marbling = !marbled && model != null && MarbleCards.HasMarbling(model);
        Sync(parent, frame, MarbleName, marbled, () => _marbleMaterial ??= Material(MarbleShader, frame.Size), onTop: true);
        Sync(parent, frame, MarblingName, marbling, () =>
        {
            if (_marblingMaterial == null)
            {
                _marblingMaterial = Material(MarbleShader, frame.Size);
                _marblingMaterial.SetShaderParameter("strength", 0.45f);
            }

            return _marblingMaterial;
        }, onTop: true);
    }
}
