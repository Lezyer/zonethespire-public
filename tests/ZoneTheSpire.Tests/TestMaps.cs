using System.Collections.Generic;
using System.Linq;
using ZoneTheSpire.Core.Generation;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Tests;

internal static class TestMaps
{
    private static readonly NodeKind[] RoomKinds =
    {
        NodeKind.Unknown, NodeKind.Shop, NodeKind.Treasure, NodeKind.RestSite, NodeKind.Monster, NodeKind.Elite,
    };

    /// <summary>Fully filled grid; every node links straight up to the next row.</summary>
    public static MapGraph Grid(int cols, int rows, NodeKind kind = NodeKind.Monster)
    {
        var nodes = new List<MapNode>();
        var edges = new List<(GridCoord, GridCoord)>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                nodes.Add(new MapNode(new GridCoord(col, row), kind));
                if (row + 1 < rows)
                {
                    edges.Add((new GridCoord(col, row), new GridCoord(col, row + 1)));
                }
            }
        }

        return new MapGraph(nodes, edges);
    }

    /// <summary>STS-like 7x15 map with ~60% of cells filled and mixed room types, generated from a seed.</summary>
    public static MapGraph Sparse(ulong layoutSeed)
    {
        var rng = new ZoneRandom(layoutSeed);
        var nodes = new List<MapNode>();
        for (int row = 0; row < 15; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                if (rng.NextInt(100) < 60)
                {
                    nodes.Add(new MapNode(new GridCoord(col, row), RoomKinds[rng.NextInt(RoomKinds.Length)]));
                }
            }
        }

        var coords = nodes.Select(n => n.Coord).ToList();
        var edges = coords
            .SelectMany(from => coords
                .Where(to => to.Row == from.Row + 1 && System.Math.Abs(to.Col - from.Col) <= 1)
                .Select(to => (from, to)))
            .ToList();
        return new MapGraph(nodes, edges);
    }

    /// <summary>Same graph content, supplied to the constructor in a different order.</summary>
    public static MapGraph Shuffled(MapGraph graph, ulong shuffleSeed)
    {
        var rng = new ZoneRandom(shuffleSeed);
        var nodes = graph.Nodes.ToList();
        var edges = graph.Edges.ToList();
        for (int i = nodes.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (nodes[i], nodes[j]) = (nodes[j], nodes[i]);
        }

        for (int i = edges.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (edges[i], edges[j]) = (edges[j], edges[i]);
        }

        return new MapGraph(nodes, edges);
    }
}
