using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Model;

public sealed record MapNode(GridCoord Coord, NodeKind Kind);

/// <summary>
/// Canonical, engine-free snapshot of an act map. Nodes and edges are always sorted, so every consumer
/// iterates in the same order on every multiplayer peer regardless of how the input was enumerated.
/// </summary>
public sealed class MapGraph
{
    public MapGraph(IEnumerable<MapNode> nodes, IEnumerable<(GridCoord From, GridCoord To)> edges)
    {
        Nodes = nodes
            .GroupBy(node => node.Coord)
            .Select(group => group.OrderBy(node => (int)node.Kind).First())
            .OrderBy(node => node.Coord)
            .ToList();

        Edges = edges
            .Distinct()
            .OrderBy(edge => edge.From)
            .ThenBy(edge => edge.To)
            .ToList();
    }

    public IReadOnlyList<MapNode> Nodes { get; }

    public IReadOnlyList<(GridCoord From, GridCoord To)> Edges { get; }
}
