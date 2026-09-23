using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Devas;

/// <summary>Deva's Domain mechanic text, written once: the zone glossary and the powers' and modifiers' own tips use these strings.</summary>
public static class DevasText
{
    public static string Samsara => ModText.Get("devas.samsara");
    public static string DevasBlessing => ModText.Get("devas.devas_blessing");
    public static string Chakra => ModText.Get("devas.chakra");
    public static string Karma => ModText.Get("devas.karma");

    public static string SamsaraDescription => ModText.Get("devas.samsara_description");

    public static string DevasBlessingDescription => ModText.Get("devas.devas_blessing_description");

    public static string ChakraDescription => ModText.Get("devas.chakra_description");

    public static string KarmaDescription => ModText.Get("devas.karma_description");

    /// <summary>Only on a card that costs 0.</summary>
    public static string KarmaZeroCostNote => ModText.Get("devas.karma_zero_cost_note");

    /// <summary>Only on an X-cost card.</summary>
    public static string KarmaXCostNote => ModText.Get("devas.karma_x_cost_note");
}
