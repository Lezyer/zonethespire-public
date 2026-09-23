using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Local-only Phantasmal Tombs shop treatment. The merchant inventory's SlotsContainer is the rug TextureRect itself;
/// all merchandise, prices and service controls are children rendered separately above it. Applying this material to the
/// TextureRect therefore colors only the rug pixels, with the result clipped to the rug texture's original alpha.
/// </summary>
internal static class MerchantRugEctoplasm
{
    private const string AppliedMetaKey = "zonethespire_ectoplasm_rug";

    private const string ShaderCode = @"shader_type canvas_item;

float ecto_blob(vec2 uv, vec2 center, vec2 radius, float phase) {
    vec2 q = (uv - center) / radius;
    float angle = atan(q.y, q.x);
    float wobble = 1.0
        + 0.11 * sin(angle * 3.0 + phase)
        + 0.055 * sin(angle * 7.0 - phase * 0.73);
    float distance_to_edge = length(q) / wobble;
    return 1.0 - smoothstep(0.72, 1.0, distance_to_edge);
}

void fragment() {
    vec4 rug = texture(TEXTURE, UV);
    float t = TIME * 0.42;
    vec2 drift_a = vec2(0.018 * sin(t * 0.73), 0.012 * cos(t * 0.61));
    vec2 drift_b = vec2(0.014 * cos(t * 0.57), 0.014 * sin(t * 0.67));

    // Overlapping lobes form slowly breathing ectoplasm puddles spread from the rug's upper edge to its lower fringe.
    float blobs = 0.0;
    blobs = max(blobs, ecto_blob(UV, vec2(0.24, 0.25) + drift_a, vec2(0.115, 0.075), t));
    blobs = max(blobs, ecto_blob(UV, vec2(0.32, 0.29) + drift_a, vec2(0.082, 0.058), t + 1.7));
    blobs = max(blobs, ecto_blob(UV, vec2(0.67, 0.43) + drift_b, vec2(0.135, 0.088), t + 3.1));
    blobs = max(blobs, ecto_blob(UV, vec2(0.75, 0.47) + drift_b, vec2(0.082, 0.06), t + 4.4));
    blobs = max(blobs, ecto_blob(UV, vec2(0.36, 0.63) - drift_a, vec2(0.14, 0.092), t + 2.2));
    blobs = max(blobs, ecto_blob(UV, vec2(0.46, 0.66) - drift_a, vec2(0.08, 0.06), t + 5.0));
    blobs = max(blobs, ecto_blob(UV, vec2(0.72, 0.82) + drift_a, vec2(0.13, 0.082), t + 0.9));
    blobs = max(blobs, ecto_blob(UV, vec2(0.81, 0.85) + drift_a, vec2(0.075, 0.055), t + 3.8));

    float body = smoothstep(0.08, 0.72, blobs);
    float rim = smoothstep(0.04, 0.34, blobs) * (1.0 - smoothstep(0.48, 0.88, blobs));
    float inner_glow = smoothstep(0.52, 0.96, blobs);
    float shimmer = 0.78 + 0.22 * sin(TIME * 1.7 + UV.x * 21.0 + UV.y * 13.0);
    vec3 ectoplasm = mix(vec3(0.10, 0.58, 0.38), vec3(0.52, 1.0, 0.72), inner_glow);

    vec3 color = mix(rug.rgb, rug.rgb * 0.52 + ectoplasm * 0.58, body * 0.72);
    color += ectoplasm * (rim * 0.38 + inner_glow * 0.18 * shimmer);
    COLOR = vec4(color, rug.a);
}
";

    private static Shader? _shader;

    public static void Apply(NMerchantInventory inventoryNode, MerchantInventory inventory)
    {
        try
        {
            if (!ZoneEffectQuery.IsActive(inventory.Player.RunState, PhantasmalTombsBiome.RaidedShopEffectId)
                || inventoryNode.GetNodeOrNull<TextureRect>("%SlotsContainer") is not { } rug
                || rug.HasMeta(AppliedMetaKey))
            {
                return;
            }

            rug.Material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
            rug.SetMeta(AppliedMetaKey, true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply ectoplasm to the merchant rug: {ex}");
        }
    }
}
