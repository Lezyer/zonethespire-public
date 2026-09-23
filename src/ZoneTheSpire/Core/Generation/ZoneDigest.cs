using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Generation;

/// <summary>Order-independent hash of an act's zones, logged on every peer to diagnose multiplayer mismatches.</summary>
public static class ZoneDigest
{
    public static uint Compute(int actIndex, IEnumerable<Zone> zones)
    {
        var text = new StringBuilder();
        text.Append(StableHash.Inv(actIndex)).Append('|');
        foreach (var zone in zones.OrderBy(zone => zone.Id, StringComparer.Ordinal))
        {
            text.Append(zone.Id).Append(':').Append(zone.BiomeId).Append(':');
            foreach (var coord in zone.Nodes.OrderBy(coord => coord))
            {
                text.Append(StableHash.Inv(coord.Row)).Append(',').Append(StableHash.Inv(coord.Col)).Append(';');
            }

            text.Append('/');
        }

        return StableHash.Fold32(StableHash.Fnv1a64(text.ToString()));
    }
}
