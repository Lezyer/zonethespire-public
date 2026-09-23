using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Map;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Run;

/// <summary>Converts a game ActMap to the engine-free MapGraph. MapGraph sorts everything, so HashSet order here is harmless.</summary>
internal static class MapGraphBuilder
{
    public static MapGraph Build(ActMap map)
    {
        var excluded = new HashSet<MapCoord> { map.StartingMapPoint.coord, map.BossMapPoint.coord };
        if (map.SecondBossMapPoint != null)
        {
            excluded.Add(map.SecondBossMapPoint.coord);
        }

        List<MapPoint> points = map.GetAllMapPoints().Where(point => !excluded.Contains(point.coord)).ToList();
        IEnumerable<MapNode> nodes = points.Select(point => new MapNode(ToGrid(point.coord), NodeKinds.From(point.PointType)));
        IEnumerable<(GridCoord From, GridCoord To)> edges = points.SelectMany(point => point.Children
            .Where(child => !excluded.Contains(child.coord))
            .Select(child => (ToGrid(point.coord), ToGrid(child.coord))));

        return new MapGraph(nodes, edges);
    }

    private static GridCoord ToGrid(MapCoord coord) => new(coord.col, coord.row);
}
