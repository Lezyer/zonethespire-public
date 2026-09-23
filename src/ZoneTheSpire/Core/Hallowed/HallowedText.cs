using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Hallowed;

/// <summary>
/// Blinding Hallows's player-facing mechanic text, written once: the zone glossary (HallowedBiome.Keywords) and the powers'
/// and card modifiers' own hover tips use these same strings. Numbers come from <see cref="HallowedRules"/>.
/// </summary>
public static class HallowedText
{
    public static string Hallowed => ModText.Get("hallowed.hallowed");
    public static string Blasphemer => ModText.Get("hallowed.blasphemer");
    public static string Blasphemous => ModText.Get("hallowed.blasphemous");
    public static string Zealous => ModText.Get("hallowed.zealous");
    public static string Hallowing => ModText.Get("hallowed.hallowing");
    public static string Redemption => ModText.Get("hallowed.redemption");

    public static string HallowedDescription => ModText.Get("hallowed.hallowed_description");

    /// <summary>Only matters in combat, where Doom and Hallowed meet: on the power's own tip, not in the map glossary.</summary>
    public static string HallowedDoomNote => ModText.Get("hallowed.hallowed_doom_note");

    public static string BlasphemerDescription => ModText.Get("hallowed.blasphemer_description");

    public static string BlasphemousDescription => ModText.Get("hallowed.blasphemous_description");

    /// <summary>"Even when blocked" stays on enemy effects: players expect Block to protect them from what enemies do.</summary>
    public static string ZealousDescription => ModText.Get("hallowed.zealous_description");

    /// <summary>Like vanilla Blight Strike: "damage dealt" already counts blocked damage, so it goes unsaid on your own cards.</summary>
    public static string HallowingDescription => ModText.Get("hallowed.hallowing_description");

    public static string RedemptionDescription => ModText.Get("hallowed.redemption_description");

    /// <summary>The on-card line of a keyword: its name alone, explained by the card's hover tip.</summary>
    public static string CardLine(string keyword) => $"[gold]{keyword}[/gold].";
}
