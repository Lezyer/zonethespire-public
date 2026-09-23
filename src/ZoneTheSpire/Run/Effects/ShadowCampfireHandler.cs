using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Shadow Corruption campfires: Rest becomes Troubled Dreams, and Mend is removed.</summary>
internal sealed class ShadowCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => ShadowCorruptionBiome.TroubledDreamsEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options is not IList<RestSiteOption> list)
        {
            Log.Warn("Rest site options are not an indexable list; Troubled Dreams skipped.");
            return false;
        }

        bool modified = false;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is HealRestSiteOption)
            {
                list[i] = new TroubledDreamsRestSiteOption(player);
                modified = true;
            }
            else if (list[i] is MendRestSiteOption)
            {
                list.RemoveAt(i);
                modified = true;
            }
        }

        return modified;
    }
}
