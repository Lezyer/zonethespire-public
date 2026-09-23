using System;
using System.Collections.Generic;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Core.Scrapyard;

/// <summary>
/// Places floating creatures (Scrapyard bots) one at a time without overlapping the creatures already on screen or each
/// other. Rendering-only, deterministic. Coordinates match <see cref="CloneLayout"/> (feet Y, negative is up).
/// </summary>
public static class FloatingPlacement
{
    /// <summary>Feet Y a bot uses when nothing is below it.</summary>
    public const float PreferredFeetY = -60f;

    /// <summary>Space kept between a bot and the top of a creature below it (room for that creature's intent).</summary>
    public const float VerticalGap = 120f;

    /// <summary>Minimum horizontal space between a bot and a creature beside it.</summary>
    public const float HorizontalGap = 20f;

    private const float Step = 10f;
    private const float Epsilon = 0.01f;

    /// <summary>
    /// For each bot in order: the on-screen X (sampled every 10px) where it needs the least raising above
    /// <see cref="PreferredFeetY"/> to clear everything below it, ties broken by closeness to an evenly spread target X.
    /// A placed bot becomes an obstacle for the next. When no spot fits under the stage top, the bot goes to its target X
    /// as high as the screen allows.
    /// </summary>
    public static IReadOnlyList<(float X, float Y)> Place(IReadOnlyList<CreatureBox> occupied, IReadOnlyList<CloneShape> bots, StageBounds stage)
    {
        var obstacles = new List<CreatureBox>(occupied);
        var placements = new List<(float X, float Y)>(bots.Count);
        for (int i = 0; i < bots.Count; i++)
        {
            CloneShape bot = bots[i];
            float half = bot.Width / 2f;
            float low = stage.MinX + half;
            float high = MathF.Max(low, stage.MaxX - half);
            float targetX = low + (high - low) * (i + 1) / (bots.Count + 1);

            (float X, float Y)? best = null;
            float bestRaise = float.MaxValue;
            float bestDistance = float.MaxValue;
            for (float x = low; x <= high + Epsilon; x += Step)
            {
                float y = PreferredFeetY;
                foreach (CreatureBox box in obstacles)
                {
                    if (MathF.Abs(x - box.CenterX) < half + box.Width / 2f + HorizontalGap)
                    {
                        y = MathF.Min(y, box.Top - VerticalGap - bot.TopOffset - bot.Height);
                    }
                }

                if (y + bot.TopOffset < stage.MinTop - Epsilon)
                {
                    continue;
                }

                float raise = PreferredFeetY - y;
                float distance = MathF.Abs(x - targetX);
                if (raise < bestRaise - Epsilon || (MathF.Abs(raise - bestRaise) <= Epsilon && distance < bestDistance - Epsilon))
                {
                    best = (x, y);
                    bestRaise = raise;
                    bestDistance = distance;
                }
            }

            (float X, float Y) spot = best ?? (targetX, stage.MinTop - bot.TopOffset);
            placements.Add(spot);
            obstacles.Add(new CreatureBox(spot.X, spot.Y, bot.Width, bot.TopOffset, bot.Height));
        }

        return placements;
    }
}
