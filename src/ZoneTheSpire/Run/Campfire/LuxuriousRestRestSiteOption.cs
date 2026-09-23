using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rewards;
using ZoneTheSpire.Core.Midas;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Halls of Midas Luxurious Rest: pay 50 gold, then the vanilla rest with twice the heal (the heal amount still goes through
/// the rest heal hooks, so relics that change it apply before doubling), followed by the vanilla after-rest hooks and rest
/// rewards. Without 50 gold it can still be chosen but does nothing. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class LuxuriousRestRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanilla;

    public LuxuriousRestRestSiteOption(Player owner)
        : base(owner)
    {
        _vanilla = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.LuxuriousRestId;

    public override IEnumerable<string> AssetPaths => _vanilla.AssetPaths;

    /// <summary>Set by OnSelect when the rest actually happened; without it the rest VFX are skipped.</summary>
    private bool _rested;

    /// <summary>
    /// Same variables as HealRestSiteOption.Description, with the heal doubled. Without 50 gold the text adds a red warning
    /// that choosing it does nothing (the option stays clickable so a player who can't Smith is never stuck at the fire).
    /// </summary>
    public override LocString Description
    {
        get
        {
            LocString description = HallsOfMidasRules.CanAffordLuxuriousRest(Owner.Gold)
                ? base.Description
                : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionNoGold");
            var heal = new HealVar(HallsOfMidasRules.LuxuriousHealAmount(HealRestSiteOption.GetBaseHealAmount(Owner.Creature)))
            {
                PreviewValue = HallsOfMidasRules.LuxuriousHealAmount(HealRestSiteOption.GetHealAmount(Owner)),
            };
            description.Add("Character", Owner.Character.Id.Entry);
            description.Add(heal);
            IReadOnlyList<LocString> extra = Hook.ModifyExtraRestSiteHealText(Owner.RunState, Owner, Array.Empty<LocString>());
            description.Add("ExtraText", extra.Any() ? "\n" + string.Join("\n", extra.Select(text => text.GetFormattedText())) : string.Empty);
            return description;
        }
    }

    public override async Task<bool> OnSelect()
    {
        if (!HallsOfMidasRules.CanAffordLuxuriousRest(Owner.Gold))
        {
            // Counts as the rest site action (so the player can leave), but pays and heals nothing.
            return true;
        }

        _rested = true;

        await PlayerCmd.LoseGold(HallsOfMidasRules.LuxuriousRestGoldCost, Owner, GoldLossType.Spent);
        decimal amount = HallsOfMidasRules.LuxuriousHealAmount(HealRestSiteOption.GetHealAmount(Owner));
        await CreatureCmd.Heal(Owner.Creature, amount);

        // The rest of HealRestSiteOption.ExecuteRestSiteHeal: after-rest hooks and rest rewards.
        await Hook.AfterRestSiteHeal(Owner.RunState, Owner, isMimicked: false);
        var rewards = new List<Reward>();
        Hook.ModifyRestSiteHealRewards(Owner.RunState, Owner, rewards, isMimicked: false);
        await RewardsCmd.OfferCustom(Owner, rewards);
        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default) =>
        _rested ? _vanilla.DoLocalPostSelectVfx(ct) : Task.CompletedTask;

    public override Task DoRemotePostSelectVfx() => _rested ? _vanilla.DoRemotePostSelectVfx() : Task.CompletedTask;
}
