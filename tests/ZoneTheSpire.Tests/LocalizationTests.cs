using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Tests;

public class LocalizationTests
{
    private static readonly IReadOnlyDictionary<string, string> Numbers = new Dictionary<string, string>
    {
        ["HallowedRules.RepentHpCost"] = "10",
        ["FermentoryRules.BottlePrice"] = "75",
    };

    [Fact]
    public void Registry_HasEveryRulesConstant_AsInvariantText()
    {
        var registry = PlaceholderRegistry.Default;
        Assert.Equal("10", registry["HallowedRules.RepentHpCost"]);
        Assert.Equal("75", registry["FermentoryRules.BottlePrice"]);
        Assert.Equal("15", registry["HallowedRules.ZealousPercent"]);
        Assert.Contains("ZoneRelicEffects.SacrosanctFlailAttacks", registry.Keys);
        Assert.DoesNotContain(registry.Keys, key => !key.Contains('.'));
    }

    [Fact]
    public void Resolve_FillsConstantsAndKeys_AndLeavesGameVariables()
    {
        var keys = new Dictionary<string, string> { ["keyword.bottle.name"] = "Bottle" };
        string text = LocTemplate.Resolve(
            "Pay [red]{FermentoryRules.BottlePrice}[/red]. A [gold]{@keyword.bottle.name}[/gold] costs {Amount}, {Count:plural:card|cards}.",
            Numbers,
            key => keys.GetValueOrDefault(key),
            out List<string> errors);

        Assert.Equal("Pay [red]75[/red]. A [gold]Bottle[/gold] costs {Amount}, {Count:plural:card|cards}.", text);
        Assert.Empty(errors);
    }

    [Fact]
    public void Resolve_FollowsKeyReferencesInsideKeys_AndStopsOnCycles()
    {
        var keys = new Dictionary<string, string>
        {
            ["a"] = "A costs {HallowedRules.RepentHpCost} and {@b}",
            ["b"] = "B",
            ["loop"] = "{@loop}",
        };

        Assert.Equal("A costs 10 and B", LocTemplate.Resolve("{@a}", Numbers, key => keys.GetValueOrDefault(key), out var errors));
        Assert.Empty(errors);

        LocTemplate.Resolve("{@loop}", Numbers, key => keys.GetValueOrDefault(key), out var loopErrors);
        Assert.NotEmpty(loopErrors);
    }

    [Fact]
    public void Resolve_ReportsUnknownPlaceholdersAndKeys()
    {
        string text = LocTemplate.Resolve("{NoSuch.Constant} {@no.such.key}", Numbers, _ => null, out List<string> errors);
        Assert.Equal(2, errors.Count);
        Assert.Equal("{NoSuch.Constant} {@no.such.key}", text);
    }

    [Fact]
    public void Fill_ReplacesNamedRuntimeValues()
    {
        Assert.Equal(
            "After acting, it deals [blue]12[/blue] damage to you.",
            LocTemplate.Fill("After acting, it deals [blue]{Amount}[/blue] damage to {Players}.", ("Amount", 12), ("Players", "you")));
    }

    [Fact]
    public void ModText_UsesItsSource_AndShowsTheKeyWhenMissing()
    {
        // The tests' source is the English files (TestText); the global source is never swapped, tests run in parallel.
        Assert.Equal("Mirrored", ModText.Get("mirror.mirrored"));
        Assert.Equal(
            "The first time it drops to half HP, it spawns a [gold]Reflection[/gold] with [blue]{Amount}%[/blue] of its Max HP.",
            ModText.Get("mirror.mirrored_smart_description"));
        Assert.Equal(
            "The first time it drops to half HP, it spawns a [gold]Reflection[/gold] with [blue]50%[/blue] of its Max HP.",
            ModText.Format("mirror.mirrored_smart_description", ("Amount", 50)));
        Assert.Equal("no.such.key", ModText.Get("no.such.key"));
    }

    [Fact]
    public void LocFiles_ReadFlatJsonObjects()
    {
        var entries = LocFiles.Parse("{\n  \"a.b\": \"One\\nTwo\",\n  \"c\": \"[gold]x[/gold]\"\n}");
        Assert.Equal("One\nTwo", entries["a.b"]);
        Assert.Equal(new[] { "a.b", "c" }, entries.Keys.OrderBy(k => k));
    }
}

public class LocalizedCoreTextTests
{
    [Fact]
    public void EnemyPotionLines_ReadAsBefore()
    {
        Assert.Equal(
            "After acting, it deals [blue]12[/blue] damage to ALL players.",
            ZoneTheSpire.Core.Fermentory.FermentoryText.EnemyPotionLine(ZoneTheSpire.Core.Fermentory.EnemyPotion.Fire, 12, multiplayer: true));
        Assert.Equal(
            "After acting, it applies [blue]1[/blue] [gold]Weak[/gold] to you.",
            ZoneTheSpire.Core.Fermentory.FermentoryText.EnemyPotionLine(ZoneTheSpire.Core.Fermentory.EnemyPotion.Weak, 1, multiplayer: false));
        Assert.Equal("Refilling Still", ZoneTheSpire.Core.Fermentory.FermentoryText.Name(ZoneTheSpire.Core.Fermentory.SlotEffect.RefillingStill));
        Assert.Equal(
            "When you use a potion from this slot, heal [blue]5[/blue] HP.",
            ZoneTheSpire.Core.Fermentory.FermentoryText.Description(ZoneTheSpire.Core.Fermentory.SlotEffect.HealingBalm));
    }
}
