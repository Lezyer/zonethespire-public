using System.Collections.Generic;
using Xunit;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Scrapyard;

namespace ZoneTheSpire.Tests;

public class FloatingPlacementTests
{
    private static readonly StageBounds Stage = new(150f, 960f, -430f);

    private static CreatureBox Enemy(float centerX, float width) => new(centerX, 200f, width, -250f, 250f);

    private static bool Overlap(CreatureBox a, CreatureBox b) =>
        System.MathF.Abs(a.CenterX - b.CenterX) < (a.Width + b.Width) / 2f
        && a.Top < b.Top + b.Height
        && b.Top < a.Top + a.Height;

    [Fact]
    public void Place_SpreadsTwoBotsWithoutOverlap_WhenNothingIsBelow()
    {
        var bot = new CloneShape(200f, -220f, 220f);

        IReadOnlyList<(float X, float Y)> spots = FloatingPlacement.Place(new CreatureBox[0], new[] { bot, bot }, Stage);

        Assert.Equal((450f, FloatingPlacement.PreferredFeetY), spots[0]);
        Assert.Equal((670f, FloatingPlacement.PreferredFeetY), spots[1]);
    }

    [Fact]
    public void Place_RaisesBotAboveEnemies_KeepingTheIntentGap_WhenEnemiesCoverTheRow()
    {
        CreatureBox left = Enemy(300f, 350f);
        CreatureBox right = Enemy(650f, 350f);
        var bot = new CloneShape(150f, -150f, 150f);

        (float X, float Y) spot = FloatingPlacement.Place(new[] { left, right }, new[] { bot }, Stage)[0];

        Assert.Equal(555f, spot.X);
        Assert.Equal(left.Top - FloatingPlacement.VerticalGap, spot.Y);
    }

    [Fact]
    public void Place_NeverOverlapsEnemiesOrOtherBots_AndStaysOnScreen_InACrowdedRow()
    {
        var enemies = new[] { Enemy(250f, 200f), Enemy(480f, 200f), Enemy(710f, 200f), Enemy(880f, 150f) };
        var bots = new[] { new CloneShape(180f, -200f, 200f), new CloneShape(180f, -200f, 200f) };

        IReadOnlyList<(float X, float Y)> spots = FloatingPlacement.Place(enemies, bots, Stage);

        var placed = new List<CreatureBox>();
        for (int i = 0; i < spots.Count; i++)
        {
            var box = new CreatureBox(spots[i].X, spots[i].Y, bots[i].Width, bots[i].TopOffset, bots[i].Height);
            Assert.True(box.CenterX - box.Width / 2f >= Stage.MinX - 0.01f && box.CenterX + box.Width / 2f <= Stage.MaxX + 0.01f);
            Assert.True(box.Top >= Stage.MinTop - 0.01f, $"bot {i} top {box.Top} is off-screen");
            foreach (CreatureBox enemy in enemies)
            {
                Assert.False(Overlap(box, enemy), $"bot {i} overlaps an enemy");
            }

            foreach (CreatureBox other in placed)
            {
                Assert.False(Overlap(box, other), $"bot {i} overlaps another bot");
            }

            placed.Add(box);
        }
    }
}
