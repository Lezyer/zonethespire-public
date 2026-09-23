using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Mirrorlands rest sites: Rest → Mirrored Rest, Mend → Mirrored Mend, replaced in place so option indices (synced by
/// OptionIndexChosenMessage) stay the same on every peer. Other options are untouched.
/// </summary>
internal sealed class MirroredCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => MirrorlandsBiome.MirroredCampfireEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options is not IList<RestSiteOption> list)
        {
            Log.Warn("Rest site options are not an indexable list; Mirrorlands campfire options skipped.");
            return false;
        }

        bool modified = false;
        for (int i = 0; i < list.Count; i++)
        {
            RestSiteOption? replacement = list[i] switch
            {
                HealRestSiteOption => new MirroredRestRestSiteOption(player),
                MendRestSiteOption => new MirroredMendRestSiteOption(player),
                _ => null,
            };
            if (replacement != null)
            {
                list[i] = replacement;
                modified = true;
            }
        }

        return modified;
    }
}
