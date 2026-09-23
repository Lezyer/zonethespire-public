using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ZoneTheSpire.Core.Localization;

/// <summary>
/// The mod's own text (table <c>zone_the_spire</c>) for code that builds tooltips itself. In game the source is the game's
/// table for the current language (English where a string is missing); in tests it is the English file. A missing key shows
/// the key, which is how the game shows missing text too.
/// </summary>
public static class ModText
{
    private static Func<string, string?>? _source;
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static Func<string, string?>? Source
    {
        get => _source;
        set
        {
            _source = value;
            Reset();
        }
    }

    /// <summary>Forgets cached text (the language changed).</summary>
    public static void Reset()
    {
        lock (Cache)
        {
            Cache.Clear();
        }
    }

    public static string Get(string key)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out string? cached))
            {
                return cached;
            }
        }

        string text = _source?.Invoke(key) ?? key;
        lock (Cache)
        {
            Cache[key] = text;
        }

        return text;
    }

    public static string Format(string key, params (string Name, object Value)[] values) => LocTemplate.Fill(Get(key), values);
}

/// <summary>Reads the mod's flat <c>{ "key": "text" }</c> JSON text files.</summary>
public static class LocFiles
{
    public static Dictionary<string, string> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
}
