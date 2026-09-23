using System.Linq;
using Xunit;

namespace ZoneTheSpire.Tests;

public class LocalizationSnapshotTests
{
    [Fact]
    public void EnglishFiles_ReproduceTheSnapshot_StringForString()
    {
        var (text, _) = LocalizationFiles.ResolvedEnglish();
        var snapshot = LocalizationFiles.Snapshot();

        var missing = snapshot.Keys.Where(key => !text.ContainsKey(key)).ToList();
        Assert.True(missing.Count == 0, "Missing from the English files:\n" + string.Join("\n", missing));

        var different = snapshot.Where(pair => text[pair.Key] != pair.Value)
            .Select(pair => $"{pair.Key}\n  snapshot: {pair.Value}\n  files:    {text[pair.Key]}")
            .ToList();
        Assert.True(different.Count == 0, "Different from the snapshot:\n" + string.Join("\n", different));
    }

    [Fact]
    public void EnglishFiles_ResolveEveryPlaceholderAndKey()
    {
        var (_, errors) = LocalizationFiles.ResolvedEnglish();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(pair => $"{pair.Key}: {string.Join("; ", pair.Value)}")));
    }
}
