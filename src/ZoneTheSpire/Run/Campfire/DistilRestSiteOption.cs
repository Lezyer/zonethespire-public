using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Run.Fermentory;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// The Fermentory Distil (extra rest site option, counts as the campfire's action): the leftmost normal potion slot becomes
/// special (random effect, seeded per location and player), then a reward screen offers 50 Gold and a potion. With every slot
/// already special it still gives both. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class DistilRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanillaAssets;

    public DistilRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.DistilId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    /// <summary>Always usable; the description says so when no slot can change.</summary>
    public override LocString Description => SpecialSlots.CanGain(Owner)
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        SpecialSlots.MakeNextSpecial(Owner, "Distil");
        var rewards = new List<Reward>
        {
            new GoldReward(FermentoryRules.DistilGold, FermentoryRules.DistilGold, Owner),
            new PotionReward(Owner),
        };
        await RewardsCmd.OfferCustom(Owner, rewards);
        return true;
    }
}
