using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models.Modifiers;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Infestation rest sites: Smith → Festering Smith, replaced in place (keeping its smith count) so option indices (synced by
/// OptionIndexChosenMessage) stay the same on every peer. Other options are untouched.
/// </summary>
internal sealed class InfestationCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => InfestationBiome.FesteringSmithEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        // The Midas run modifier removes Smith by type; if it runs after this, a Festering Smith would survive. Leave Smith
        // alone in Midas runs so it is removed as vanilla intends.
        if (player.RunState.Modifiers.Any(modifier => modifier is Midas))
        {
            return false;
        }

        if (options is not IList<RestSiteOption> list)
        {
            Log.Warn("Rest site options are not an indexable list; Festering Smith skipped.");
            return false;
        }

        bool modified = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is SmithRestSiteOption smith)
            {
                list[i] = new FesteringSmithRestSiteOption(player, smith.SmithCount);
                modified = true;
            }
        }

        return modified;
    }
}
