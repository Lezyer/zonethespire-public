using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ZoneTheSpire.Core.Localization;

/// <summary>
/// Placeholders in the mod's text files. <c>{ClassName.ConstantName}</c> is a rules constant (PlaceholderRegistry) and
/// <c>{@key}</c> is another text of the mod's own table in the same language; both are filled when a file is loaded.
/// Anything else in braces (<c>{Amount}</c>, <c>{Count:plural:card|cards}</c>) is a runtime variable and is left for the game
/// or for <see cref="Fill"/>.
/// </summary>
public static class LocTemplate
{
    private static readonly Regex Constant = new(@"\{([A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant);
    private static readonly Regex KeyReference = new(@"\{@([A-Za-z0-9_.\-]+)\}", RegexOptions.CultureInvariant);
    private static readonly Regex Named = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Fills <paramref name="text"/>'s constants and key references (key references recursively, each key's text resolved the
    /// same way). Unknown constants, missing keys and cycles are left as written and reported in <paramref name="errors"/>.
    /// </summary>
    public static string Resolve(
        string text,
        IReadOnlyDictionary<string, string> constants,
        Func<string, string?> keyLookup,
        out List<string> errors)
    {
        var found = new List<string>();
        string result = Resolve(text, constants, keyLookup, found, new HashSet<string>(StringComparer.Ordinal));
        errors = found;
        return result;
    }

    private static string Resolve(
        string text,
        IReadOnlyDictionary<string, string> constants,
        Func<string, string?> keyLookup,
        List<string> errors,
        HashSet<string> visiting)
    {
        string withKeys = KeyReference.Replace(text, match =>
        {
            string key = match.Groups[1].Value;
            if (!visiting.Add(key))
            {
                errors.Add($"key reference cycle at '{key}'");
                return match.Value;
            }

            try
            {
                string? referenced = keyLookup(key);
                if (referenced == null)
                {
                    errors.Add($"unknown key '{key}'");
                    return match.Value;
                }

                return Resolve(referenced, constants, keyLookup, errors, visiting);
            }
            finally
            {
                visiting.Remove(key);
            }
        });

        return Constant.Replace(withKeys, match =>
        {
            if (constants.TryGetValue(match.Groups[1].Value, out string? value))
            {
                return value;
            }

            errors.Add($"unknown placeholder '{match.Value}'");
            return match.Value;
        });
    }

    /// <summary>Fills runtime <c>{Name}</c> variables (invariant culture); names without a value are left as written.</summary>
    public static string Fill(string text, params (string Name, object Value)[] values)
    {
        if (values.Length == 0)
        {
            return text;
        }

        var lookup = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string name, object value) in values)
        {
            lookup[name] = value;
        }

        return Named.Replace(text, match =>
            lookup.TryGetValue(match.Groups[1].Value, out object? value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : match.Value);
    }
}
