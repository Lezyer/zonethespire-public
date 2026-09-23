using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Hoarfrost;

/// <summary>Hoarfrost mechanic text, written once: the zone glossary and the powers' and modifier's own tips use these strings.</summary>
public static class FrostText
{
    public static string Hoarfrost => ModText.Get("frost.hoarfrost");
    public static string Frozen => ModText.Get("frost.frozen");
    public static string BitingCold => ModText.Get("frost.biting_cold");

    public static string HoarfrostDescription => ModText.Get("frost.hoarfrost_description");

    public static string FrozenDescription => ModText.Get("frost.frozen_description");

    /// <summary>
    /// Both senses in one entry (card rewards name the card modifier, Frostheart the power): what it does to an enemy, then
    /// how a card applies it.
    /// </summary>
    public static string BitingColdDescription => ModText.Get("frost.biting_cold_description");

    /// <summary>The power on an iced enemy.</summary>
    public static string BitingColdPowerDescription => ModText.Get("frost.biting_cold_power_description");
}
