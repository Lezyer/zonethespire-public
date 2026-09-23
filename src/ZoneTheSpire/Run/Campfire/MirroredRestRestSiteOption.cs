using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>Mirrorlands Rest: the vanilla rest (heal, hooks, rest rewards), then an unskippable 1-of-3 duplicate from your deck.</summary>
public sealed class MirroredRestRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanilla;

    /// <summary>Offered cards, fixed when the rest site options are created (see MirrorDuplication.SnapshotOffer).</summary>
    private readonly IReadOnlyList<CardModel> _offers;

    public MirroredRestRestSiteOption(Player owner)
        : base(owner)
    {
        _vanilla = new HealRestSiteOption(owner);
        _offers = MirrorDuplication.SnapshotOffer(owner, owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.MirroredHealId;

    public override IEnumerable<string> AssetPaths => _vanilla.AssetPaths;

    /// <summary>Same variables as HealRestSiteOption.Description.</summary>
    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            var heal = new HealVar(HealRestSiteOption.GetBaseHealAmount(Owner.Creature))
            {
                PreviewValue = HealRestSiteOption.GetHealAmount(Owner),
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
        await HealRestSiteOption.ExecuteRestSiteHeal(Owner, isMimicked: false);
        await MirrorDuplication.OfferSnapshot(Owner, _offers);
        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default) => _vanilla.DoLocalPostSelectVfx(ct);

    public override Task DoRemotePostSelectVfx() => _vanilla.DoRemotePostSelectVfx();
}
