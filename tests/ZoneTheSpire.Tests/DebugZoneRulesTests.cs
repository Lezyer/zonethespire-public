using System;
using System.Linq;
using System.Collections.Generic;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Debug;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneEvents;
using Xunit;

namespace ZoneTheSpire.Tests;

public class DebugZoneRulesTests
{
    [Theory]
    [InlineData("mirrorlands", "mirrorlands")]
    [InlineData("Mirrorlands", "mirrorlands")]
    [InlineData("  PRISMATIC_STORM ", "prismatic_storm")]
    [InlineData("scrapyard", "scrapyard")]
    public void ResolveBiome_MatchesIdIgnoringCaseAndWhitespace(string input, string expectedId)
    {
        Assert.Equal(expectedId, DebugZoneRules.ResolveBiome(input)?.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mirror")]
    [InlineData("The Scrapyard")]
    public void ResolveBiome_ReturnsNull_ForUnknownZones(string input)
    {
        Assert.Null(DebugZoneRules.ResolveBiome(input));
    }

    [Theory]
    [InlineData(DebugEncounterKind.Monster, NodeKind.Monster)]
    [InlineData(DebugEncounterKind.Elite, NodeKind.Elite)]
    [InlineData(DebugEncounterKind.Boss, NodeKind.Elite)]
    public void NodeKindFor_MapsEncounterKind_BossCountsAsElite(DebugEncounterKind encounter, NodeKind expected)
    {
        Assert.Equal(expected, DebugZoneRules.NodeKindFor(encounter));
    }

    [Fact]
    public void ZoneIdList_NamesEveryBiome()
    {
        Assert.Equal("scrapyard, mirrorlands, infestation, prismatic_storm, blood_rain, halls_of_midas, phantasmal_tombs, ferrosand, forgotten_empire, shadow_corruption, devas_domain, hoarfrost, blinding_hallowed, fermentory", DebugZoneRules.ZoneIdList);
        Assert.Equal(BiomeRegistry.All.Count, DebugZoneRules.ZoneIdList.Split(", ").Length);
    }

    [Theory]
    [InlineData("shop", DebugRoomKind.Shop)]
    [InlineData(" Merchant ", DebugRoomKind.Shop)]
    [InlineData("rest", DebugRoomKind.RestSite)]
    [InlineData("CAMPFIRE", DebugRoomKind.RestSite)]
    [InlineData("rest_site", DebugRoomKind.RestSite)]
    [InlineData("treasure", DebugRoomKind.Treasure)]
    [InlineData(" Chest", DebugRoomKind.Treasure)]
    public void ResolveRoom_AcceptsShopRestAndTreasureAliases(string input, DebugRoomKind expected)
    {
        Assert.Equal(expected, DebugZoneRules.ResolveRoom(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("elite")]
    [InlineData("boss")]
    public void ResolveRoom_ReturnsNull_ForOtherRooms(string input)
    {
        Assert.Null(DebugZoneRules.ResolveRoom(input));
    }

    [Theory]
    [InlineData(DebugRoomKind.Shop, NodeKind.Shop)]
    [InlineData(DebugRoomKind.RestSite, NodeKind.RestSite)]
    [InlineData(DebugRoomKind.Treasure, NodeKind.Treasure)]
    public void NodeKindFor_MapsRoomKinds(DebugRoomKind room, NodeKind expected)
    {
        Assert.Equal(expected, DebugZoneRules.NodeKindFor(room));
    }

    [Fact]
    public void RoomIdList_NamesTheCanonicalRoomIds()
    {
        Assert.Equal(new[] { "shop", "rest", "treasure" }, DebugZoneRules.RoomIds);
        Assert.Equal("shop, rest, treasure", DebugZoneRules.RoomIdList);
    }

    [Fact]
    public void ForcedEvent_AlwaysPicksAZoneEvent_PreferringUnseen_ThenAnyWhenAllSeen()
    {
        var candidates = new[]
        {
            new ZoneEventCandidate("A", new[] { "blood_rain" }),
            new ZoneEventCandidate("B", new[] { "blood_rain", "halls_of_midas" }),
            new ZoneEventCandidate("C", new[] { "ferrosand" }),
        };

        for (int attempt = 0; attempt < 20; attempt++)
        {
            string key = "debug:0:" + attempt;
            Assert.Equal("B", DebugZoneRules.PickForcedEvent(7UL, key, "blood_rain", candidates, new HashSet<string> { "A" }));
            Assert.Contains(DebugZoneRules.PickForcedEvent(7UL, key, "blood_rain", candidates, new HashSet<string> { "A", "B" }), new[] { "A", "B" });
            Assert.Contains(DebugZoneRules.PickForcedEvent(7UL, key, "blood_rain", candidates, new HashSet<string>()), new[] { "A", "B" });
        }

        Assert.Null(DebugZoneRules.PickForcedEvent(7UL, "debug:0:0", "shadow_corruption", candidates, new HashSet<string>()));
    }

    [Fact]
    public void AnyEvent_PicksEveryEventEvenly_AndOneOfItsZones()
    {
        var candidates = new[]
        {
            new ZoneEventCandidate("A", new[] { "blood_rain" }),
            new ZoneEventCandidate("B", new[] { "blood_rain", "halls_of_midas" }),
            new ZoneEventCandidate("C", new[] { "ferrosand" }),
        };
        var counts = new Dictionary<string, int>();
        for (int attempt = 0; attempt < 3000; attempt++)
        {
            var pick = DebugZoneRules.PickAnyEvent(7UL, "random:" + attempt, candidates)!.Value;
            Assert.Contains(pick.BiomeId, candidates.Single(candidate => candidate.Key == pick.Key).BiomeIds);
            string bucket = pick.Key + "@" + pick.BiomeId;
            counts[bucket] = counts.GetValueOrDefault(bucket) + 1;
        }

        Assert.InRange(counts["A@blood_rain"], 850, 1150);
        Assert.InRange(counts["C@ferrosand"], 850, 1150);
        Assert.InRange(counts["B@blood_rain"], 400, 600);
        Assert.InRange(counts["B@halls_of_midas"], 400, 600);
        Assert.Equal(DebugZoneRules.PickAnyEvent(7UL, "random:5", candidates), DebugZoneRules.PickAnyEvent(7UL, "random:5", candidates));
        Assert.Null(DebugZoneRules.PickAnyEvent(7UL, "random:0", Array.Empty<ZoneEventCandidate>()));
    }
}
