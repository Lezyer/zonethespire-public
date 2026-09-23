using System.Collections.Generic;

namespace ZoneTheSpire.Core.Persistence;

/// <summary>Root JSON document stored in the hidden modifier's [SavedProperty] ZonesJson.</summary>
public sealed class ZoneSetRecord
{
    public int Version { get; set; } = ZoneSerializer.CurrentVersion;

    public SortedDictionary<int, ActZoneRecord> Acts { get; set; } = new();
}

public sealed class ActZoneRecord
{
    public string MapFingerprint { get; set; } = string.Empty;

    public List<ZoneRecord> Zones { get; set; } = new();
}

public sealed class ZoneRecord
{
    public string Id { get; set; } = string.Empty;

    public string Biome { get; set; } = string.Empty;

    /// <summary>Each entry is [col, row].</summary>
    public List<int[]> Nodes { get; set; } = new();
}
