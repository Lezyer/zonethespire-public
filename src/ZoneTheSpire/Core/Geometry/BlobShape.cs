using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ZoneTheSpire.Core.Geometry;

/// <summary>
/// Smooth zone outline that hugs only the member nodes: a circle around each member and a narrower bridge along each
/// link between adjacent members, blended with a smooth union, traced with marching squares and rounded with Chaikin
/// smoothing. The radius stays well under half the node spacing, so non-member nodes and neighbouring zones stay
/// outside. Rendering-only (local); output is stable for the same input.
/// </summary>
public static class BlobShape
{
    public const float DefaultRadius = 58f;

    private const float BridgeWidthFactor = 0.75f;
    private const float Blend = 24f;
    private const float CellSize = 4f;
    private const float MinPointSpacing = 2f;
    private const int SmoothingPasses = 2;

    public static List<Vector2> Build(IReadOnlyList<Vector2> centers, IReadOnlyList<(int A, int B)> links, float radius = DefaultRadius)
    {
        if (centers.Count == 0)
        {
            return new List<Vector2>();
        }

        float bridgeWidth = radius * BridgeWidthFactor;
        float padding = radius + Blend + CellSize * 2f;
        float minX = centers.Min(c => c.X) - padding;
        float minY = centers.Min(c => c.Y) - padding;
        int columns = (int)MathF.Ceiling((centers.Max(c => c.X) + padding - minX) / CellSize) + 1;
        int rows = (int)MathF.Ceiling((centers.Max(c => c.Y) + padding - minY) / CellSize) + 1;

        var field = new float[columns, rows];
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                field[x, y] = Distance(new Vector2(minX + x * CellSize, minY + y * CellSize), centers, links, radius, bridgeWidth);
            }
        }

        List<Vector2> contour = TraceLongestContour(field, columns, rows, minX, minY);
        return Smooth(RemoveNearDuplicates(contour));
    }

    /// <summary>Signed distance to the smooth union of member circles and link bridges (negative inside).</summary>
    private static float Distance(Vector2 p, IReadOnlyList<Vector2> centers, IReadOnlyList<(int A, int B)> links, float radius, float bridgeWidth)
    {
        float d = float.MaxValue;
        foreach (Vector2 center in centers)
        {
            d = SmoothMin(d, Vector2.Distance(p, center) - radius);
        }

        foreach (var (a, b) in links)
        {
            if (a >= 0 && b >= 0 && a < centers.Count && b < centers.Count)
            {
                d = SmoothMin(d, DistanceToSegment(p, centers[a], centers[b]) - bridgeWidth);
            }
        }

        return d;
    }

    private static float SmoothMin(float a, float b)
    {
        if (a == float.MaxValue)
        {
            return b;
        }

        float h = MathF.Max(Blend - MathF.Abs(a - b), 0f) / Blend;
        return MathF.Min(a, b) - h * h * Blend * 0.25f;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared <= 0.0001f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Marching squares over the field's zero level. Crossing points are keyed by the grid edge they lie on, so shared
    /// points join segments into loops. Returns the longest loop (the outer outline).
    /// </summary>
    private static List<Vector2> TraceLongestContour(float[,] field, int columns, int rows, float minX, float minY)
    {
        var points = new Dictionary<long, Vector2>();
        var neighbours = new Dictionary<long, List<long>>();

        long HorizontalEdge(int x, int y) => ((long)y * columns + x) * 2;
        long VerticalEdge(int x, int y) => ((long)y * columns + x) * 2 + 1;

        Vector2 Crossing(int x0, int y0, int x1, int y1)
        {
            float v0 = field[x0, y0];
            float v1 = field[x1, y1];
            float t = MathF.Abs(v1 - v0) < 1e-6f ? 0.5f : v0 / (v0 - v1);
            return new Vector2(minX + (x0 + (x1 - x0) * t) * CellSize, minY + (y0 + (y1 - y0) * t) * CellSize);
        }

        void Link(long from, long to)
        {
            if (!neighbours.TryGetValue(from, out List<long>? fromList))
            {
                neighbours[from] = fromList = new List<long>();
            }

            if (!neighbours.TryGetValue(to, out List<long>? toList))
            {
                neighbours[to] = toList = new List<long>();
            }

            fromList.Add(to);
            toList.Add(from);
        }

        for (int y = 0; y < rows - 1; y++)
        {
            for (int x = 0; x < columns - 1; x++)
            {
                bool topLeft = field[x, y] < 0f;
                bool topRight = field[x + 1, y] < 0f;
                bool bottomRight = field[x + 1, y + 1] < 0f;
                bool bottomLeft = field[x, y + 1] < 0f;

                var crossings = new List<long>(4);
                void AddCrossing(long key, int x0, int y0, int x1, int y1)
                {
                    if (!points.ContainsKey(key))
                    {
                        points[key] = Crossing(x0, y0, x1, y1);
                    }

                    crossings.Add(key);
                }

                long top = HorizontalEdge(x, y);
                long bottom = HorizontalEdge(x, y + 1);
                long left = VerticalEdge(x, y);
                long right = VerticalEdge(x + 1, y);

                if (topLeft != topRight) AddCrossing(top, x, y, x + 1, y);
                if (topRight != bottomRight) AddCrossing(right, x + 1, y, x + 1, y + 1);
                if (bottomLeft != bottomRight) AddCrossing(bottom, x, y + 1, x + 1, y + 1);
                if (topLeft != bottomLeft) AddCrossing(left, x, y, x, y + 1);

                if (crossings.Count == 2)
                {
                    Link(crossings[0], crossings[1]);
                }
                else if (crossings.Count == 4)
                {
                    // Saddle: resolve with the cell-centre value so the inside regions stay connected consistently.
                    float centre = (field[x, y] + field[x + 1, y] + field[x + 1, y + 1] + field[x, y + 1]) * 0.25f;
                    bool centreInside = centre < 0f;
                    if (centreInside == topLeft)
                    {
                        Link(top, right);
                        Link(bottom, left);
                    }
                    else
                    {
                        Link(top, left);
                        Link(right, bottom);
                    }
                }
            }
        }

        var visited = new HashSet<long>();
        List<Vector2> longest = new();
        foreach (long start in neighbours.Keys.OrderBy(key => key))
        {
            if (visited.Contains(start))
            {
                continue;
            }

            var loop = new List<Vector2>();
            long previous = -1;
            long current = start;
            while (visited.Add(current))
            {
                loop.Add(points[current]);
                long next = neighbours[current].FirstOrDefault(n => n != previous && !visited.Contains(n), -1);
                if (next < 0)
                {
                    break;
                }

                previous = current;
                current = next;
            }

            if (loop.Count > longest.Count)
            {
                longest = loop;
            }
        }

        return longest;
    }

    private static List<Vector2> RemoveNearDuplicates(List<Vector2> loop)
    {
        var result = new List<Vector2>(loop.Count);
        foreach (Vector2 point in loop)
        {
            if (result.Count == 0 || Vector2.Distance(result[^1], point) >= MinPointSpacing)
            {
                result.Add(point);
            }
        }

        while (result.Count > 3 && Vector2.Distance(result[0], result[^1]) < MinPointSpacing)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    /// <summary>Chaikin corner cutting on a closed loop.</summary>
    private static List<Vector2> Smooth(List<Vector2> loop)
    {
        if (loop.Count < 3)
        {
            return loop;
        }

        List<Vector2> current = loop;
        for (int pass = 0; pass < SmoothingPasses; pass++)
        {
            var next = new List<Vector2>(current.Count * 2);
            for (int i = 0; i < current.Count; i++)
            {
                Vector2 a = current[i];
                Vector2 b = current[(i + 1) % current.Count];
                next.Add(Vector2.Lerp(a, b, 0.25f));
                next.Add(Vector2.Lerp(a, b, 0.75f));
            }

            current = next;
        }

        return current;
    }
}
