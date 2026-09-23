using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Run;

/// <summary>The zone, biome and node kind of a map node, used to decide which biome effects apply there.</summary>
public sealed record ZoneContext(Zone Zone, BiomeDefinition Biome, NodeKind NodeKind)
{
    public IReadOnlyList<BiomeEffect> Effects => ZoneEffects.EffectsFor(Biome, NodeKind);

    public static ZoneContext? For(IRunState runState, int actIndex, MapCoord coord, MapPointType pointType)
    {
        NodeKind kind = NodeKinds.From(pointType);
        if (kind == NodeKind.Other)
        {
            return null;
        }

        Zone? zone = ZoneService.GetZoneAt(runState, actIndex, coord);
        BiomeDefinition? biome = zone == null ? null : BiomeRegistry.Get(zone.BiomeId);
        return zone == null || biome == null ? null : new ZoneContext(zone, biome, kind);
    }

    /// <summary>
    /// Context of the node the party is currently at, unless a
    /// <c>fight_with_zone</c>, <c>room_with_zone</c> or <c>event_with_zone</c>(<c>_random</c>) debug room has overridden it.
    /// A "?" node that resolved to a shop or a chest counts as that room once it is entered (so it gets the zone's shop or
    /// chest effects); a "?" that became an event or a fight stays Unknown. Rooms are left before the next one is created,
    /// so while a "?" rolls its event the previous room is never mistaken for it.
    /// </summary>
    public static ZoneContext? Current(IRunState runState)
    {
        if (ZoneOverride.ContextFor(runState) is { } overridden)
        {
            return overridden;
        }

        if (runState.CurrentMapCoord is not { } coord)
        {
            return null;
        }

        MapPoint? point = runState.Map?.GetPoint(coord);
        ZoneContext? context = point == null ? null : For(runState, runState.CurrentActIndex, coord, point.PointType);
        return context == null ? null : context with { NodeKind = context.NodeKind.ResolvedBy(NodeKinds.FromRoom(runState.CurrentRoom)) };
    }
}
