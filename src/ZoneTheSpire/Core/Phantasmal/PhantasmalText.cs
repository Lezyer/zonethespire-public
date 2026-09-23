using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Phantasmal;

/// <summary>Phantasmal Tombs mechanic text, written once: the zone glossary and the power's and modifier's own tips use these strings.</summary>
public static class PhantasmalText
{
    public static string Phantasm => ModText.Get("phantasmal.phantasm");
    public static string PhantasmHaunted => ModText.Get("phantasmal.phantasm_haunted");

    public static string PhantasmDescription => ModText.Get("phantasmal.phantasm_description");

    public static string PhantasmHauntedDescription => ModText.Get("phantasmal.phantasm_haunted_description");

    /// <summary>X-cost cards: the only place the X bonus is mentioned.</summary>
    public static string PhantasmHauntedXDescription => ModText.Get("phantasmal.phantasm_haunted_x_description");
}
