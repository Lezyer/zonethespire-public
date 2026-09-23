using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Tests;

/// <summary>The mod's text files as copied into the test output, resolved the way the game resolves them.</summary>
internal static class LocalizationFiles
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Localization");

    public static IEnumerable<string> Languages() =>
        Directory.GetDirectories(Root).Select(Path.GetFileName).OfType<string>();

    /// <summary>One language's raw files: table name -> key -> text.</summary>
    public static Dictionary<string, Dictionary<string, string>> Raw(string language) =>
        Directory.GetFiles(Path.Combine(Root, language), "*.json")
            .ToDictionary(Path.GetFileNameWithoutExtension, path => LocFiles.Parse(File.ReadAllText(path)))!;

    /// <summary>English, resolved: "table|key" -> text, with the errors of each unresolvable entry.</summary>
    public static (Dictionary<string, string> Text, Dictionary<string, List<string>> Errors) ResolvedEnglish()
    {
        var tables = Raw("eng");
        var own = tables.GetValueOrDefault("zone_the_spire") ?? new Dictionary<string, string>();
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach ((string table, var entries) in tables)
        {
            foreach ((string key, string value) in entries)
            {
                string resolved = LocTemplate.Resolve(value, PlaceholderRegistry.Default, k => own.GetValueOrDefault(k), out List<string> found);
                text[$"{table}|{key}"] = resolved;
                if (found.Count > 0)
                {
                    errors[$"{table}|{key}"] = found;
                }
            }
        }

        return (text, errors);
    }

    public static Dictionary<string, string> Snapshot() =>
        LocFiles.Parse(File.ReadAllText(Path.Combine(Root, "snapshot.eng.json")));
}
