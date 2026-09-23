using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models.Modifiers;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Ferrosand rest sites: an extra Magnetize option after the existing ones. Appended (never inserted) so the vanilla option
/// indices synced by OptionIndexChosenMessage stay the same on every peer.
/// </summary>
internal sealed class FerrosandCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => FerrosandBiome.MagnetizeEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        options.Add(new MagnetizeRestSiteOption(player));
        return true;
    }
}
