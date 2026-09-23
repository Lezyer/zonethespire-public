using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Blood Rain rest sites: an extra Blood Sacrifice option is appended after the existing ones, so every peer builds the same
/// option list (indices synced by OptionIndexChosenMessage stay aligned).
/// </summary>
internal sealed class BloodRainCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => BloodRainBiome.BloodSacrificeEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options.Any(option => option is BloodSacrificeRestSiteOption))
        {
            return false;
        }

        options.Add(new BloodSacrificeRestSiteOption(player));
        return true;
    }
}
