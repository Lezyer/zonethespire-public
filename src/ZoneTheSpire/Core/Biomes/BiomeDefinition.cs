using System;
using System.Collections.Generic;

namespace ZoneTheSpire.Core.Biomes;

/// <summary>
/// One biome. Ids are persisted in saves and used in multiplayer-deterministic generation: never rename an Id.
/// </summary>
public abstract class BiomeDefinition
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    /// <summary>RGB as 6 uppercase hex digits without '#', e.g. "8B5A2B".</summary>
    public abstract string ColorHex { get; }

    /// <summary>Effects this biome applies to its member nodes. Each effect declares which node kinds it affects.</summary>
    public virtual IReadOnlyList<BiomeEffect> Effects => Array.Empty<BiomeEffect>();

    /// <summary>
    /// Colour of the zone name in node tooltips (6 hex digits), when the map colour is too dark to read on the tooltip background;
    /// null uses the map outline colour.
    /// </summary>
    public virtual string? TooltipColorHex => null;

    /// <summary>
    /// Colour of the zone's fill on the map (6 hex digits), when it differs from <see cref="ColorHex"/>, which still colours the
    /// border and the zone name; null fills with <see cref="ColorHex"/>.
    /// </summary>
    public virtual string? MapFillColorHex => null;

    /// <summary>
    /// The mechanics this zone introduces, each explained once. A map node tooltip shows a tip for every keyword its text names in
    /// [gold] (and for keywords named in those tips), so effect lines name keywords instead of explaining them.
    /// </summary>
    public virtual IReadOnlyList<ZoneKeyword> Keywords => Array.Empty<ZoneKeyword>();

    /// <summary>A line shown on the tooltip of every node of this zone, after the effect lines; may contain {Zone} and other {Token}s.</summary>
    public virtual string? TooltipFooter => null;
}
