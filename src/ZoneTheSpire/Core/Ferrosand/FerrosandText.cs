using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Ferrosand;

/// <summary>Ferrosand mechanic text, written once: the zone glossary and the powers' and modifier's own tips use these strings.</summary>
public static class FerrosandText
{
    public static string Magnetized => ModText.Get("ferrosand.magnetized");
    public static string Ferroform => ModText.Get("ferrosand.ferroform");
    public static string Magnetic => ModText.Get("ferrosand.magnetic");

    public static string MagnetizedDescription => ModText.Get("ferrosand.magnetized_description");

    public static string MagnetizedSmartDescription => ModText.Get("ferrosand.magnetized_smart_description");

    public static string FerroformDescription => ModText.Get("ferrosand.ferroform_description");

    public static string MagneticDescription => ModText.Get("ferrosand.magnetic_description");
}
