using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ZoneTheSpire.Core.Localization;

/// <summary>
/// The <c>{ClassName.ConstantName}</c> placeholders the mod's text files may use: every public numeric constant of the mod's
/// rules classes (public static classes in <c>ZoneTheSpire.Core</c> whose name ends in "Rules" or "Effects"), found by
/// reflection so a new constant is usable at once. Values are written with the invariant culture.
/// </summary>
public static class PlaceholderRegistry
{
    private static IReadOnlyDictionary<string, string>? _default;

    public static IReadOnlyDictionary<string, string> Default => _default ??= Build(typeof(PlaceholderRegistry).Assembly);

    public static IReadOnlyDictionary<string, string> Build(Assembly assembly)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Type type in assembly.GetTypes().Where(IsRulesClass))
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if ((field.IsLiteral || field.IsInitOnly) && Format(field.GetValue(null)) is { } text)
                {
                    values[type.Name + "." + field.Name] = text;
                }
            }
        }

        return values;
    }

    private static bool IsRulesClass(Type type) =>
        type is { IsClass: true, IsAbstract: true, IsSealed: true, IsPublic: true }
        && type.Namespace?.StartsWith("ZoneTheSpire.Core", StringComparison.Ordinal) == true
        && (type.Name.EndsWith("Rules", StringComparison.Ordinal) || type.Name.EndsWith("Effects", StringComparison.Ordinal));

    private static string? Format(object? value) => value switch
    {
        int or long or short or byte or uint or ulong => Convert.ToString(value, CultureInfo.InvariantCulture),
        decimal or double or float => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => null,
    };
}
