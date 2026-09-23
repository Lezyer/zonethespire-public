using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Shadow;

/// <summary>Shadow Corruption mechanic text, written once: the zone glossary and the powers' and modifier's own tips use these strings.</summary>
public static class ShadowText
{
    public static string ShadowBrutality => ModText.Get("shadow.shadow_brutality");
    public static string Shaded => ModText.Get("shadow.shaded");
    public static string LightTheWay => ModText.Get("shadow.light_the_way");
    public static string ShadowCorrupted => ModText.Get("shadow.shadow_corrupted");

    public static string ShadowBrutalityDescription => ModText.Get("shadow.shadow_brutality_description");

    public static string ShadedDescription => ModText.Get("shadow.shaded_description");

    public static string LightTheWayDescription => ModText.Get("shadow.light_the_way_description");

    /// <summary>The Doom it costs depends on the card's cost, so the card shows the number; the tip doesn't repeat it.</summary>
    public static string ShadowCorruptedDescription => ModText.Get("shadow.shadow_corrupted_description");
}
