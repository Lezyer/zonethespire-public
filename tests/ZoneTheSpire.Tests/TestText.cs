using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Tests;

/// <summary>Core reads its text through ModText; in tests that is the English files, resolved like the game resolves them.</summary>
internal static class TestText
{
    [ModuleInitializer]
    internal static void UseEnglishFiles()
    {
        Dictionary<string, string> own = LocalizationFiles.Raw("eng").GetValueOrDefault("zone_the_spire") ?? new Dictionary<string, string>();
        string? Lookup(string key) =>
            own.TryGetValue(key, out string? text) ? LocTemplate.Resolve(text, PlaceholderRegistry.Default, Lookup, out _) : null;

        ModText.Source = Lookup;
    }
}
