using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Tests;

/// <summary>
/// Every translation folder next to eng: keys English has, the same placeholders and key references, balanced tags, and
/// text that resolves. A language may leave keys out (the game falls back to English per string).
/// </summary>
public class TranslationTests
{
    private readonly ITestOutputHelper _output;

    public TranslationTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex Token = new(@"\{[^{}]+\}");
    private static readonly Regex Tag = new(@"\[(/?)([a-z_]+)(?:[ =][^\]]*)?\]");

    /// <summary>What is wrong with one language's tables compared to English's; empty when nothing is.</summary>
    internal static List<string> Problems(
        Dictionary<string, Dictionary<string, string>> language, Dictionary<string, Dictionary<string, string>> english)
    {
        var problems = new List<string>();
        var ownEnglish = english.GetValueOrDefault("zone_the_spire") ?? new Dictionary<string, string>();
        var own = language.GetValueOrDefault("zone_the_spire") ?? new Dictionary<string, string>();
        foreach ((string table, var entries) in language)
        {
            if (!english.TryGetValue(table, out var englishEntries))
            {
                problems.Add($"{table}: no such English table");
                continue;
            }

            foreach ((string key, string text) in entries)
            {
                string where = $"{table}|{key}";
                if (!englishEntries.TryGetValue(key, out string? englishText))
                {
                    problems.Add($"{where}: not an English key");
                    continue;
                }

                var tokens = Tokens(text);
                var englishTokens = Tokens(englishText);
                if (!tokens.SetEquals(englishTokens))
                {
                    problems.Add($"{where}: placeholders {string.Join(" ", tokens.OrderBy(t => t))} differ from English's {string.Join(" ", englishTokens.OrderBy(t => t))}");
                }

                if (UnbalancedTags(text) is { } tagProblem)
                {
                    problems.Add($"{where}: {tagProblem}");
                }

                LocTemplate.Resolve(text, PlaceholderRegistry.Default, k => own.GetValueOrDefault(k) ?? ownEnglish.GetValueOrDefault(k), out List<string> errors);
                problems.AddRange(errors.Select(error => $"{where}: {error}"));
            }
        }

        return problems;
    }

    private static HashSet<string> Tokens(string text) => Token.Matches(text).Select(match => match.Value).ToHashSet();

    /// <summary>Null when every [tag] closes in order, else what is wrong.</summary>
    internal static string? UnbalancedTags(string text)
    {
        var open = new Stack<string>();
        foreach (Match match in Tag.Matches(text))
        {
            string name = match.Groups[2].Value;
            if (match.Groups[1].Value.Length == 0)
            {
                open.Push(name);
            }
            else if (open.Count == 0 || open.Pop() != name)
            {
                return $"[/{name}] closes nothing open";
            }
        }

        return open.Count == 0 ? null : $"[{open.Peek()}] never closes";
    }

    [Fact]
    public void EveryTranslation_MatchesEnglish()
    {
        var english = LocalizationFiles.Raw("eng");
        int englishCount = english.Values.Sum(entries => entries.Count);
        var problems = new List<string>();
        foreach (string language in LocalizationFiles.Languages().Where(language => language != "eng"))
        {
            var tables = LocalizationFiles.Raw(language);
            int translated = tables.Values.Sum(entries => entries.Count);
            _output.WriteLine($"{language}: {translated}/{englishCount} strings ({100.0 * translated / englishCount:0.#}%)");
            problems.AddRange(Problems(tables, english).Select(problem => $"{language}/{problem}"));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void English_TagsAreBalanced()
    {
        var problems = LocalizationFiles.Raw("eng")
            .SelectMany(table => table.Value.Select(entry => (Where: $"{table.Key}|{entry.Key}", Problem: UnbalancedTags(entry.Value))))
            .Where(item => item.Problem != null)
            .Select(item => $"{item.Where}: {item.Problem}")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static Dictionary<string, Dictionary<string, string>> Tables(params (string Table, string Key, string Text)[] entries) =>
        entries.GroupBy(entry => entry.Table).ToDictionary(group => group.Key, group => group.ToDictionary(entry => entry.Key, entry => entry.Text));

    private static readonly Dictionary<string, Dictionary<string, string>> English = Tables(
        ("zone_the_spire", "frost.frozen", "Frozen"),
        ("zone_the_spire", "frost.line", "Gain [blue]{Amount}[/blue] [gold]{@frost.frozen}[/gold]."),
        ("powers", "X.title", "{@frost.frozen}"));

    [Fact]
    public void Problems_None_ForAFaithfulPartialTranslation()
    {
        var french = Tables(("zone_the_spire", "frost.line", "Gagnez [blue]{Amount}[/blue] [gold]{@frost.frozen}[/gold]."));
        Assert.Empty(Problems(french, English));
    }

    [Fact]
    public void Problems_Found_ForUnknownKeysChangedPlaceholdersAndBrokenTags()
    {
        var broken = Tables(
            ("zone_the_spire", "frost.nope", "?"),
            ("zone_the_spire", "frost.line", "Gagnez [blue]{Amont}[/blue] [gold]{@frost.frozen}."),
            ("potions", "Y.title", "?"));
        var problems = Problems(broken, English);
        Assert.Contains(problems, problem => problem.StartsWith("zone_the_spire|frost.nope: not an English key"));
        Assert.Contains(problems, problem => problem.StartsWith("zone_the_spire|frost.line: placeholders"));
        Assert.Contains(problems, problem => problem.StartsWith("zone_the_spire|frost.line: [gold] never closes"));
        Assert.Contains(problems, problem => problem.StartsWith("potions: no such English table"));
    }
}
