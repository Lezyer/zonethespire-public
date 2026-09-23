using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// A zone glossary entry: a mechanic the zone introduces (a power or card modifier), explained once. Map node tooltips show
/// a tip for each keyword their text names as [gold]Name[/gold] (see ZoneTooltip.KeywordsIn). <paramref name="Key"/> is the
/// text key of its name (zone_the_spire table): the name follows the game's language, the key never changes.
/// </summary>
public sealed record ZoneKeyword(string Key, string Description)
{
    /// <summary>The keyword's name in the current language.</summary>
    public string Name => ModText.Get(Key);
}
