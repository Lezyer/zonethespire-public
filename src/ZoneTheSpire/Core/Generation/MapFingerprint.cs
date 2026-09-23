using System.Globalization;
using System.Text;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Generation;

/// <summary>
/// Identifies a map's structure: coordinates, zone eligibility and edges. Exact point types are deliberately
/// excluded so revealing "?" rooms or other mods retyping nodes do not invalidate saved zones.
/// </summary>
public static class MapFingerprint
{
    public static string Compute(MapGraph graph)
    {
        var text = new StringBuilder();
        foreach (var node in graph.Nodes)
        {
            text.Append(StableHash.Inv(node.Coord.Row)).Append(',')
                .Append(StableHash.Inv(node.Coord.Col)).Append(',')
                .Append(node.Kind.IsZoneEligible() ? '1' : '0').Append(';');
        }

        text.Append('|');
        foreach (var (from, to) in graph.Edges)
        {
            text.Append(StableHash.Inv(from.Row)).Append(',').Append(StableHash.Inv(from.Col))
                .Append('>')
                .Append(StableHash.Inv(to.Row)).Append(',').Append(StableHash.Inv(to.Col))
                .Append(';');
        }

        return StableHash.Fnv1a64(text.ToString()).ToString("x16", CultureInfo.InvariantCulture);
    }
}
