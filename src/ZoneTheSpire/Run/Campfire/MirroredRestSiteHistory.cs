using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// RestSiteSynchronizer.ChooseOption records option.OptionId in the player's map point history (RestSiteChoices). Vanilla
/// badges (Restful, Restless, Healer) and run history visuals look for the literal "HEAL" and "MEND", so the Mirrorlands
/// ids are rewritten to those. Only these strings change; it runs from a hook that fires identically on every peer.
/// </summary>
internal static class MirroredRestSiteHistory
{
    public static void RemapChoices(IRunState runState)
    {
        try
        {
            foreach (IReadOnlyList<MapPointHistoryEntry> act in runState.MapPointHistory)
            {
                foreach (MapPointHistoryEntry entry in act)
                {
                    foreach (PlayerMapPointHistoryEntry player in entry.PlayerStats)
                    {
                        List<string> choices = player.RestSiteChoices;
                        for (int i = 0; i < choices.Count; i++)
                        {
                            choices[i] = choices[i] switch
                            {
                                RestSiteLoc.MirroredHealId => "HEAL",
                                RestSiteLoc.LuxuriousRestId => "HEAL",
                                RestSiteLoc.MirroredMendId => "MEND",
                                RestSiteLoc.FesteringSmithId => "SMITH",
                                _ => choices[i],
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to record Mirrorlands rest site choices as Rest/Mend in run history: {ex}");
        }
    }
}
