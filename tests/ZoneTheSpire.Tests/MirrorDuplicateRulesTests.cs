using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Tests;

public class MirrorDuplicateRulesTests
{
    private static readonly string Key = MirrorDuplicateRules.LocationKey(0, 5, 3, 0);

    [Fact]
    public void LocationKey_IsDeterministic()
    {
        Assert.Equal(MirrorDuplicateRules.LocationKey(1, 7, 2, 0), MirrorDuplicateRules.LocationKey(1, 7, 2, 0));
        Assert.Equal(MirrorDuplicateRules.LocationKey(1, null, null, 3), MirrorDuplicateRules.LocationKey(1, null, null, 3));
    }

    [Fact]
    public void LocationKey_ChangesWithActRowColumnAndRoom()
    {
        var keys = new[]
        {
            MirrorDuplicateRules.LocationKey(0, 5, 3, 0),
            MirrorDuplicateRules.LocationKey(1, 5, 3, 0),
            MirrorDuplicateRules.LocationKey(0, 6, 3, 0),
            MirrorDuplicateRules.LocationKey(0, 5, 4, 0),
            MirrorDuplicateRules.LocationKey(0, 5, 3, 1),
            MirrorDuplicateRules.LocationKey(0, 3, 5, 0),
            MirrorDuplicateRules.LocationKey(0, 53, null, 0),
            MirrorDuplicateRules.LocationKey(0, 5, 33, 0),
        };

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void LocationKey_DistinguishesMissingCoordinates()
    {
        var keys = new[]
        {
            MirrorDuplicateRules.LocationKey(0, null, null, 0),
            MirrorDuplicateRules.LocationKey(0, 0, 0, 0),
            MirrorDuplicateRules.LocationKey(0, 0, null, 0),
            MirrorDuplicateRules.LocationKey(0, null, 0, 0),
            MirrorDuplicateRules.LocationKey(0, null, null, 1),
        };

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void PickOffer_IsEmpty_ForEmptyDeck()
    {
        Assert.Empty(MirrorDuplicateRules.PickOffer(1UL, Key, 10UL, 10UL, 0));
    }

    [Fact]
    public void PickOffer_OffersTheOnlyCard_ForSingleCardDeck()
    {
        Assert.Equal(new[] { 0 }, MirrorDuplicateRules.PickOffer(1UL, Key, 10UL, 10UL, 1));
    }

    [Fact]
    public void PickOffer_ReturnsThreeDistinctInRangeIndices_Deterministically()
    {
        for (int row = 0; row < 300; row++)
        {
            string key = MirrorDuplicateRules.LocationKey(row % 3, row, row % 7, 0);
            IReadOnlyList<int> offer = MirrorDuplicateRules.PickOffer(99UL, key, 7UL, 8UL, 12);
            Assert.Equal(3, offer.Count);
            Assert.Equal(3, offer.Distinct().Count());
            Assert.All(offer, index => Assert.InRange(index, 0, 11));
            Assert.Equal(offer, MirrorDuplicateRules.PickOffer(99UL, key, 7UL, 8UL, 12));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PickOffer_OffersEveryCard_WhenTheDeckHasThreeOrFewer(int deckCount)
    {
        Assert.Equal(Enumerable.Range(0, deckCount), MirrorDuplicateRules.PickOffer(5UL, Key, 1UL, 2UL, deckCount).OrderBy(i => i));
    }

    [Fact]
    public void PickOffer_EveryCardCanBeOffered_InEverySlot()
    {
        var seen = new HashSet<(int Slot, int Index)>();
        for (int row = 0; row < 400; row++)
        {
            IReadOnlyList<int> offer = MirrorDuplicateRules.PickOffer(7UL, MirrorDuplicateRules.LocationKey(0, row, 1, 0), 1UL, 1UL, 5);
            for (int slot = 0; slot < offer.Count; slot++)
            {
                seen.Add((slot, offer[slot]));
            }
        }

        Assert.Equal(15, seen.Count);
    }

    [Fact]
    public void PickOffer_VariesByLocationChooserAndSource()
    {
        var byRow = new HashSet<string>();
        var byAct = new HashSet<string>();
        var byChooser = new HashSet<string>();
        var bySource = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            byRow.Add(string.Join(",", MirrorDuplicateRules.PickOffer(42UL, MirrorDuplicateRules.LocationKey(0, i, 2, 0), 1UL, 1UL, 20)));
            byAct.Add(string.Join(",", MirrorDuplicateRules.PickOffer(42UL, MirrorDuplicateRules.LocationKey(i, 4, 2, 0), 1UL, 1UL, 20)));
            byChooser.Add(string.Join(",", MirrorDuplicateRules.PickOffer(42UL, Key, (ulong)i, 1UL, 20)));
            bySource.Add(string.Join(",", MirrorDuplicateRules.PickOffer(42UL, Key, 1UL, (ulong)i, 20)));
        }

        Assert.True(byRow.Count > 10);
        Assert.True(byAct.Count > 10);
        Assert.True(byChooser.Count > 10);
        Assert.True(bySource.Count > 10);
    }

    [Fact]
    public void PickOffer_DiffersBetweenRestSitesWithTheSameRoomIdAndDeckSize()
    {
        var offers = new HashSet<string>();
        for (int row = 0; row < 15; row++)
        {
            offers.Add(string.Join(",", MirrorDuplicateRules.PickOffer(42UL, MirrorDuplicateRules.LocationKey(0, row, 1, 0), 1UL, 1UL, 20)));
        }

        Assert.True(offers.Count > 5);
    }
}
