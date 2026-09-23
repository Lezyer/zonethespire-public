using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Phantasmal Tombs rest sites: an extra Haunt option after the existing ones. Appended (never inserted) so the vanilla option
/// indices synced by OptionIndexChosenMessage stay the same on every peer.
/// </summary>
internal sealed class PhantasmalCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => PhantasmalTombsBiome.HauntEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        options.Add(new HauntRestSiteOption(player));
        return true;
    }
}
