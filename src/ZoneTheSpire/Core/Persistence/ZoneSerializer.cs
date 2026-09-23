using System.Linq;
using System.Text.Json;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Persistence;

public static class ZoneSerializer
{
    /// <summary>
    /// Save schema version. 2: generator forbids overlapping zones (v1 saves are regenerated deterministically on load).
    /// </summary>
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Serialize(ZoneSetRecord set) => JsonSerializer.Serialize(set, Options);

    /// <summary>
    /// Null/blank input is a valid empty save (returns true). Malformed JSON or an unknown schema version returns false.
    /// </summary>
    public static bool TryDeserialize(string? json, out ZoneSetRecord set)
    {
        set = new ZoneSetRecord();
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ZoneSetRecord>(json, Options);
            if (parsed?.Acts == null || parsed.Version != CurrentVersion)
            {
                return false;
            }

            set = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Zone ToZone(ZoneRecord record) => new(
        record.Id,
        record.Biome,
        (record.Nodes ?? new())
            .Where(node => node is { Length: 2 })
            .Select(node => new GridCoord(node[0], node[1]))
            .Distinct()
            .OrderBy(coord => coord)
            .ToList());

    public static ZoneRecord FromZone(Zone zone) => new()
    {
        Id = zone.Id,
        Biome = zone.BiomeId,
        Nodes = zone.Nodes.OrderBy(coord => coord).Select(coord => new[] { coord.Col, coord.Row }).ToList(),
    };
}
