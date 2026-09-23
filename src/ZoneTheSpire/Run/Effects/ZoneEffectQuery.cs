using System;
using System.Linq;
using MegaCrit.Sts2.Core.Runs;

namespace ZoneTheSpire.Run.Effects;

internal static class ZoneEffectQuery
{
    /// <summary>True when the party's current node (or the debug zone override) has the given biome effect.</summary>
    public static bool IsActive(IRunState runState, string effectId)
    {
        try
        {
            return ZoneContext.Current(runState)?.Effects.Any(effect => effect.Id == effectId) ?? false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check zone effect '{effectId}': {ex}");
            return false;
        }
    }
}
