using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// The Shadowed Path: Shadow Corruption nodes are hidden on the map until the party stands on them or on a node that leads
/// straight to them. The revealed set is saved on the run modifier and only grows. The map icon swap is local presentation;
/// the real node type never changes.
/// </summary>
internal static class ShadowPath
{
    private static string? _cachedSaved;
    private static HashSet<string> _cachedRevealed = new(StringComparer.Ordinal);

    /// <summary>Between runs (RunLifecycle).</summary>
    internal static void Reset()
    {
        _cachedSaved = null;
        _cachedRevealed = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="point"/> is a Shadow Corruption node the party hasn't revealed yet.</summary>
    public static bool IsHidden(IRunState runState, MapPoint point)
    {
        try
        {
            if (!IsShadowNode(runState, point) || ZoneService.FindModifier(runState) is not { } modifier)
            {
                return false;
            }

            return !Revealed(modifier.ShadowRevealed).Contains(ShadowRules.NodeKey(runState.CurrentActIndex, point.coord.row, point.coord.col));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check whether a map node is hidden: {ex.Message}");
            return false;
        }
    }

    /// <summary>Reveals the node the party is on and every node it leads to. Runs on every peer when a room is entered.</summary>
    public static void RevealAroundParty(IRunState runState)
    {
        try
        {
            if (runState.CurrentMapCoord is not { } coord
                || runState.Map?.GetPoint(coord) is not { } current
                || ZoneService.FindModifier(runState) is not { } modifier)
            {
                return;
            }

            List<string> keys = new[] { current }
                .Concat(current.Children)
                .Where(point => IsShadowNode(runState, point))
                .Select(point => ShadowRules.NodeKey(runState.CurrentActIndex, point.coord.row, point.coord.col))
                .ToList();
            if (keys.Count > 0)
            {
                modifier.ShadowRevealed = ShadowRules.AddToNodeSet(modifier.ShadowRevealed, keys);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to reveal Shadow Corruption nodes: {ex}");
        }
    }

    public static bool IsShadowNode(IRunState runState, MapPoint point) =>
        ZoneContext.For(runState, runState.CurrentActIndex, point.coord, point.PointType)?.Biome is ShadowCorruptionBiome;

    private static HashSet<string> Revealed(string saved)
    {
        if (!ReferenceEquals(saved, _cachedSaved))
        {
            _cachedRevealed = ShadowRules.ParseNodeSet(saved);
            _cachedSaved = saved;
        }

        return _cachedRevealed;
    }
}
