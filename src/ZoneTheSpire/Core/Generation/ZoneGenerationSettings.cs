namespace ZoneTheSpire.Core.Generation;

/// <summary>
/// Generation tuning. These values feed multiplayer-deterministic generation: if they ever become user settings they
/// must be host-authoritative and stored in the run save (spec §7.4 rule 9), never read from local config.
/// </summary>
public sealed class ZoneGenerationSettings
{
    public static ZoneGenerationSettings Default { get; } = new();

    public int MinZonesPerAct { get; init; } = 2;

    public int MaxZonesPerAct { get; init; } = 3;

    /// <summary>Row 0 is the Ancient; row 1 (the first nodes after it) never gets a zone.</summary>
    public int MinStartRow { get; init; } = 2;

    /// <summary>The first act eases in: its first three rows after the Ancient (rows 1-3) never get a zone.</summary>
    public int FirstActMinStartRow { get; init; } = 4;

    /// <summary>The lowest row a zone may cover in the act at <paramref name="actIndex"/> (0-based).</summary>
    public int MinStartRowFor(int actIndex) => actIndex == 0 ? FirstActMinStartRow : MinStartRow;

    /// <summary>Each zone's target size is rolled uniformly from MinZoneSize to MaxZoneSize (inclusive).</summary>
    public int MinZoneSize { get; init; } = 4;

    public int MaxZoneSize { get; init; } = 7;
}
