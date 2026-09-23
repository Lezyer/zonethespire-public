using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Halls of Midas rest sites: Rest → Luxurious Rest, replaced in place so option indices (synced by OptionIndexChosenMessage)
/// stay the same on every peer. Other options are untouched.
/// </summary>
internal sealed class HallsOfMidasCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => HallsOfMidasBiome.LuxuriousRestEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options is not IList<RestSiteOption> list)
        {
            Log.Warn("Rest site options are not an indexable list; Luxurious Rest skipped.");
            return false;
        }

        bool modified = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is HealRestSiteOption)
            {
                list[i] = new LuxuriousRestRestSiteOption(player);
                modified = true;
            }
        }

        return modified;
    }
}
