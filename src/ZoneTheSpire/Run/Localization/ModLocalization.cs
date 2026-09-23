using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Run.Localization;

/// <summary>
/// The mod's text files: <c>&lt;mod folder&gt;/localization/&lt;language&gt;/&lt;table&gt;.json</c>, shipped next to
/// <c>textures/</c>. The game merges them into its tables through its own mod hook (ModManager.GetModdedLocTables), so they
/// follow the game's language and, like the game's own text, fall back to English string by string. When one of these files is
/// read, <c>{ClassName.ConstantName}</c> placeholders and <c>{@key}</c> references are filled (LocTemplate); a translated
/// string with an unknown one is dropped so English shows instead.
/// </summary>
internal static class ModLocalization
{
    public const string ModTable = "zone_the_spire";
    private const string English = "eng";

    public static string Folder { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(ModLocalization).Assembly.Location) ?? string.Empty, "localization");

    /// <summary>Called from ModEntry, before the game first loads its text.</summary>
    public static void Initialize()
    {
        CustomLocTableManager.Register(ModTable);
        ModText.Source = Lookup;
        Log.Info($"Localization: languages {string.Join(", ", Languages())}.");
    }

    public static IEnumerable<string> Languages() =>
        Directory.Exists(Folder) ? Directory.GetDirectories(Folder).Select(Path.GetFileName).OfType<string>() : Array.Empty<string>();

    public static string? FileFor(string language, string file)
    {
        string path = Path.Combine(Folder, language, file);
        return File.Exists(path) ? path : null;
    }

    public static bool IsOurs(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase);

    /// <summary>A game table's text in the current language (for names the mod reuses, like an option title), or the key.</summary>
    public static string GameText(string table, string key)
    {
        try
        {
            LocTable? locTable = LocManager.Instance?.GetTable(table);
            return locTable != null && locTable.HasEntry(key) ? locTable.GetRawText(key) : key;
        }
        catch (Exception)
        {
            return key;
        }
    }

    /// <summary>The current language's text for a key of the mod's table (the game falls back to English), or null.</summary>
    private static string? Lookup(string key)
    {
        try
        {
            if (LocManager.Instance == null)
            {
                return null;
            }

            LocTable table = LocManager.Instance.GetTable(ModTable);
            return table.HasEntry(key) ? table.GetRawText(key) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Fills one loaded file's placeholders; strings that can't be filled are dropped from translations.</summary>
    public static void Resolve(string path, Dictionary<string, string> entries)
    {
        string language = new DirectoryInfo(Path.GetDirectoryName(path) ?? string.Empty).Name;
        Dictionary<string, string> ownTable = ReadTable(language);
        Dictionary<string, string> englishTable = language == English ? ownTable : ReadTable(English);
        string? KeyLookup(string key) => ownTable.TryGetValue(key, out string? own) ? own : englishTable.GetValueOrDefault(key);

        foreach (string key in entries.Keys.ToList())
        {
            string resolved = LocTemplate.Resolve(entries[key], PlaceholderRegistry.Default, KeyLookup, out List<string> errors);
            if (errors.Count == 0)
            {
                entries[key] = resolved;
                continue;
            }

            Log.Warn($"Localization: {language}/{Path.GetFileName(path)} '{key}': {string.Join("; ", errors)}.");
            if (language == English)
            {
                entries[key] = resolved;
            }
            else
            {
                entries.Remove(key);
            }
        }
    }

    private static Dictionary<string, string> ReadTable(string language)
    {
        try
        {
            string? path = FileFor(language, ModTable + ".json");
            return path == null ? new Dictionary<string, string>() : LocFiles.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warn($"Localization: couldn't read {language}/{ModTable}.json: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }
}

/// <summary>Adds the mod's file for this language and table to the ones the game merges in.</summary>
[HarmonyPatch(typeof(ModManager), nameof(ModManager.GetModdedLocTables))]
internal static class ModLocTablesPatch
{
    private static void Postfix(string language, string file, ref IEnumerable<string> __result)
    {
        try
        {
            if (ModLocalization.FileFor(language, file) is { } path)
            {
                __result = __result.Concat(new[] { path.Replace('\\', '/') }).ToList();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Localization: failed to add {language}/{file}: {ex.Message}");
        }
    }
}

/// <summary>Fills placeholders in the mod's own files as the game reads them.</summary>
[HarmonyPatch(typeof(LocManager), "LoadTable")]
internal static class ModLocLoadPatch
{
    private static void Postfix(string path, Dictionary<string, string>? __result)
    {
        try
        {
            if (__result != null && ModLocalization.IsOurs(path))
            {
                ModLocalization.Resolve(path, __result);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Localization: failed to resolve {path}: {ex.Message}");
        }
    }
}

/// <summary>The language changed: cached mod text is stale.</summary>
[HarmonyPatch(typeof(LocManager), "SetLanguageInternal")]
internal static class ModTextLanguagePatch
{
    private static void Postfix()
    {
        try
        {
            ModText.Reset();
            Rendering.NestedTooltips.ResetKeywords();
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Language change", ex);
        }
    }
}
