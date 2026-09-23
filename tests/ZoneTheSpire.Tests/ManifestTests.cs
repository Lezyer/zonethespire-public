using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace ZoneTheSpire.Tests;

public class ManifestTests
{
    private static JsonElement LoadManifest()
    {
        string path = Path.Combine(System.AppContext.BaseDirectory, "ZoneTheSpire.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    [Fact]
    public void Manifest_IdentifiesTheMod()
    {
        var manifest = LoadManifest();
        Assert.Equal("ZoneTheSpire", manifest.GetProperty("id").GetString());
        Assert.Equal("Zone the Spire", manifest.GetProperty("name").GetString());
        Assert.Equal("Reyzel", manifest.GetProperty("author").GetString());
        Assert.Matches(@"^\d+\.\d+\.\d+$", manifest.GetProperty("version").GetString());
    }

    /// <summary>The in-game description is fixed by the author and never changes; details go on the Workshop page.</summary>
    [Fact]
    public void Manifest_Description_IsTheFixedSentence()
    {
        Assert.Equal(
            "Zone the Spire adds new zones that alter gameplay with new challenges, mechanics and rewards.",
            LoadManifest().GetProperty("description").GetString());
    }

    [Fact]
    public void Manifest_LoadsDllOnly_AndAffectsGameplayForMultiplayerHandshake()
    {
        var manifest = LoadManifest();
        Assert.True(manifest.GetProperty("has_dll").GetBoolean());
        Assert.False(manifest.GetProperty("has_pck").GetBoolean());
        Assert.True(manifest.GetProperty("affects_gameplay").GetBoolean());
        Assert.Equal("0.107.1", manifest.GetProperty("min_game_version").GetString());
    }

    [Fact]
    public void Manifest_DependsOnBaseLib()
    {
        var dependencies = LoadManifest().GetProperty("dependencies").EnumerateArray().ToList();
        var baseLib = Assert.Single(dependencies);
        Assert.Equal("BaseLib", baseLib.GetProperty("id").GetString());
        Assert.Equal("3.4.7", baseLib.GetProperty("min_version").GetString());
    }
}
