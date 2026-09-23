using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Run;

internal static class NodeKinds
{
    public static NodeKind From(MapPointType type) => type switch
    {
        MapPointType.Unknown => NodeKind.Unknown,
        MapPointType.Shop => NodeKind.Shop,
        MapPointType.Treasure => NodeKind.Treasure,
        MapPointType.RestSite => NodeKind.RestSite,
        MapPointType.Monster => NodeKind.Monster,
        MapPointType.Elite => NodeKind.Elite,
        _ => NodeKind.Other,
    };

    /// <summary>The node kind a room stands for (shops and chests; everything else is Other).</summary>
    public static NodeKind FromRoom(AbstractRoom? room) => room switch
    {
        MerchantRoom => NodeKind.Shop,
        TreasureRoom => NodeKind.Treasure,
        _ => NodeKind.Other,
    };
}
