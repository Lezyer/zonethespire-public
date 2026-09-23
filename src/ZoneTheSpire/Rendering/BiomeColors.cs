using Godot;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Rendering;

internal static class BiomeColors
{
    public static Color Base(BiomeDefinition biome) => new(biome.ColorHex);

    /// <summary>Outline/ring/tooltip colour: very light biomes (Prismatic Storm) are darkened to stay readable.</summary>
    public static Color Outline(BiomeDefinition biome)
    {
        Color color = Base(biome);
        return color.Luminance > 0.85f ? color.Darkened(0.35f) : color;
    }

    /// <summary>Fill colour of the zone on the map: the biome's own map fill (Blinding Hallows: white), else its base colour.</summary>
    public static Color Fill(BiomeDefinition biome) => biome.MapFillColorHex is { } hex ? new Color(hex) : Base(biome);

    public static string OutlineHex(BiomeDefinition biome) => Outline(biome).ToHtml(false).TrimStart('#').ToUpperInvariant();

    /// <summary>Colour of the zone name in node tooltips: the biome's own tooltip colour (very dark zones), else the outline colour.</summary>
    public static string TooltipHex(BiomeDefinition biome) => biome.TooltipColorHex ?? OutlineHex(biome);
}
