using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Mirror;

/// <summary>Mirrorlands mechanic text, written once: the zone glossary and the powers' own tips use these strings.</summary>
public static class MirrorText
{
    public static string Mirrored => ModText.Get("mirror.mirrored");
    public static string Reflection => ModText.Get("mirror.reflection");

    public static string MirroredDescription => ModText.Get("mirror.mirrored_description");

    public static string MirroredSmartDescription => ModText.Get("mirror.mirrored_smart_description");

    public static string ReflectionDescription => ModText.Get("mirror.reflection_description");
}
