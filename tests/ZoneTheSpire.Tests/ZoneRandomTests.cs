using System;
using Xunit;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Tests;

public class ZoneRandomTests
{
    [Fact]
    public void NextULong_MatchesSplitMix64ReferenceSequenceForSeedZero()
    {
        var rng = new ZoneRandom(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, rng.NextULong());
        Assert.Equal(0x6E789E6AA1B965F4UL, rng.NextULong());
        Assert.Equal(0x06C45D188009454FUL, rng.NextULong());
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = ZoneRandom.ForStream(123456789UL, "zonethespire_zones_act_1");
        var b = ZoneRandom.ForStream(123456789UL, "zonethespire_zones_act_1");
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextInt(1000), b.NextInt(1000));
        }
    }

    [Fact]
    public void DifferentStreamNames_ProduceDifferentSequences()
    {
        var a = ZoneRandom.ForStream(123456789UL, "zonethespire_zones_act_1");
        var b = ZoneRandom.ForStream(123456789UL, "zonethespire_zones_act_2");
        Assert.NotEqual(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var rng = new ZoneRandom(42);
        for (int i = 0; i < 1000; i++)
        {
            int single = rng.NextInt(7);
            Assert.InRange(single, 0, 6);
            int ranged = rng.NextInt(-2, 3);
            Assert.InRange(ranged, -2, 2);
        }
    }

    [Fact]
    public void NextInt_RejectsEmptyRanges()
    {
        var rng = new ZoneRandom(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(3, 3));
    }
}
