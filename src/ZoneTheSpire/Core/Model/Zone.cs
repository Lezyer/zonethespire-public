using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Model;

/// <summary>A biome zone: a connected set of map coordinates. Nodes are always de-duplicated and sorted (row, col).</summary>
public sealed record Zone
{
    public Zone(string id, string biomeId, IEnumerable<GridCoord> nodes)
    {
        Id = id;
        BiomeId = biomeId;
        Nodes = nodes.Distinct().OrderBy(coord => coord).ToList();
    }

    public string Id { get; }

    public string BiomeId { get; }

    public IReadOnlyList<GridCoord> Nodes { get; }
}
