using Xunit;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Tests;

public class StableHashTests
{
    [Theory]
    [InlineData("", 0xcbf29ce484222325UL)]
    [InlineData("a", 0xaf63dc4c8601ec8cUL)]
    [InlineData("foobar", 0x85944171f73967e8UL)]
    public void Fnv1a64_MatchesPublishedTestVectors(string input, ulong expected)
    {
        Assert.Equal(expected, StableHash.Fnv1a64(input));
    }

    [Fact]
    public void Fold32_XorsHighAndLowHalves()
    {
        Assert.Equal(0x0000_0003u, StableHash.Fold32(0x0000_0001_0000_0002UL));
    }

    [Fact]
    public void Inv_UsesInvariantCulture()
    {
        Assert.Equal("-42", StableHash.Inv(-42));
        Assert.Equal("1000000", StableHash.Inv(1000000));
    }
}
