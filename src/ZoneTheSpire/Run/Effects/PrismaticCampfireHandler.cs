using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Prismatic Storm rest sites: an extra Stare at Prism option is appended after the existing ones, so every peer builds the
/// same option list (indices synced by OptionIndexChosenMessage stay aligned).
/// </summary>
internal sealed class PrismaticCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => PrismaticStormBiome.StareAtPrismEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options.Any(option => option is StareAtPrismRestSiteOption))
        {
            return false;
        }

        options.Add(new StareAtPrismRestSiteOption(player));
        return true;
    }
}
