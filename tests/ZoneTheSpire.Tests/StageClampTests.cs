using Xunit;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Tests;

public class StageClampTests
{
    private static readonly StageBounds Stage = new(150f, 960f, -430f);
    private static readonly CloneShape Wriggler = new(200f, -220f, 220f);

    [Fact]
    public void ClampOnStage_KeepsSpotsThatAreAlreadyOnScreen()
    {
        Assert.Equal((500f, -60f), CloneLayout.ClampOnStage(500f, -60f, Wriggler, Stage));
    }

    [Theory]
    [InlineData(2000f, 860f)]
    [InlineData(0f, 250f)]
    public void ClampOnStage_PullsTheFootprintBackInsideTheStageHorizontally(float x, float expectedX)
    {
        Assert.Equal(expectedX, CloneLayout.ClampOnStage(x, -60f, Wriggler, Stage).X);
    }

    [Fact]
    public void ClampOnStage_LowersACreatureWhoseTopIsAboveTheStage()
    {
        Assert.Equal(-210f, CloneLayout.ClampOnStage(500f, -900f, Wriggler, Stage).Y);
    }

    [Fact]
    public void ClampOnStage_PinsACreatureWiderThanTheStageToTheLeftmostValidCentre()
    {
        Assert.Equal(650f, CloneLayout.ClampOnStage(900f, -60f, new CloneShape(1000f, -220f, 220f), Stage).X);
    }
}
