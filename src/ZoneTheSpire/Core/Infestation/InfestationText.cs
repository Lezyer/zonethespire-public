using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Infestation;

/// <summary>Infestation mechanic text, written once: the zone glossary and the modifier's and power's own tips use these strings.</summary>
public static class InfestationText
{
    public static string Wriggling => ModText.Get("infestation.wriggling");
    public static string Wriggler => ModText.Get("infestation.wriggler");

    public static string WrigglingDescription => ModText.Get("infestation.wriggling_description");

    public static string WrigglerDescription => ModText.Get("infestation.wriggler_description");

    /// <summary>The same rule, on the Wriggler's own power.</summary>
    public static string WrigglerGuardDescription => ModText.Get("infestation.wriggler_guard_description");
}
