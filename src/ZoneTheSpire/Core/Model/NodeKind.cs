namespace ZoneTheSpire.Core.Model;

/// <summary>Engine-free mirror of the game's map point types that zones may cover. Everything else is Other.</summary>
public enum NodeKind
{
    Other = 0,
    Unknown,
    Shop,
    Treasure,
    RestSite,
    Monster,
    Elite,
}

public static class NodeKindExtensions
{
    public static bool IsZoneEligible(this NodeKind kind) => kind != NodeKind.Other;

    /// <summary>
    /// The kind a node counts as once its room is entered: a "?" node that turned out to be a shop or a chest gets that
    /// room's effects. Every other node (and a "?" that became an event or a fight) keeps its map kind.
    /// </summary>
    public static NodeKind ResolvedBy(this NodeKind mapKind, NodeKind roomKind) =>
        mapKind == NodeKind.Unknown && roomKind is NodeKind.Shop or NodeKind.Treasure ? roomKind : mapKind;
}
