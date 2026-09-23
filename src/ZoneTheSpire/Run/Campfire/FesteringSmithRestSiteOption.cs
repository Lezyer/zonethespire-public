using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Infestation Smith (SmithRestSiteOption is sealed, so this mirrors it): upgrade cards as usual (same prompt, hooks, smith
/// count and effects), then give each upgraded card Wriggling 2 per energy it costs, added to any Wriggling it already has.
/// Runs on every peer through the synced rest site choice, so the Wriggling amounts match everywhere.
/// </summary>
public sealed class FesteringSmithRestSiteOption : RestSiteOption
{
    private readonly SmithRestSiteOption _vanilla;
    private IEnumerable<CardModel>? _selection;

    public FesteringSmithRestSiteOption(Player owner, int smithCount)
        : base(owner)
    {
        _vanilla = new SmithRestSiteOption(owner) { SmithCount = smithCount };
        SmithCount = smithCount;
        RestSiteLoc.EnsureInjected();
    }

    public int SmithCount { get; }

    public override string OptionId => RestSiteLoc.FesteringSmithId;

    public override IEnumerable<string> AssetPaths => _vanilla.AssetPaths;

    public override LocString Description
    {
        get
        {
            if (!IsEnabled)
            {
                return new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");
            }

            var description = new LocString("rest_site_ui", "OPTION_" + OptionId + ".description");
            description.Add("Count", SmithCount);
            return description;
        }
    }

    public override bool IsEnabled => Owner.Deck.UpgradableCardCount != 0;

    public override async Task<bool> OnSelect()
    {
        var prefs = new CardSelectorPrefs(RestSiteLoc.FesteringSmithPrompt, SmithCount)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        _selection = (await CardSelectCmd.FromDeckForUpgrade(Owner, prefs)).ToList();
        if (!_selection.Any())
        {
            return false;
        }

        foreach (CardModel card in _selection)
        {
            CardCmd.Upgrade(card, CardPreviewStyle.None);
            int amount = WrigglingRules.SmithAmount(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX);
            WrigglingModifier.AddOrIncrease(card, amount);
        }

        await Hook.AfterRestSiteSmith(Owner.RunState, Owner);
        return true;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardSmithVfx.Create((_selection ?? Enumerable.Empty<CardModel>()).ToArray()));
        await Cmd.CustomScaledWait(1f, 2f, ignoreCombatEnd: false, ct);
    }

    public override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? character = NRestSiteRoom.Instance?.Characters.FirstOrDefault(c => c.Player == Owner);
        NCardSmithVfx? vfx = NCardSmithVfx.Create();
        if (vfx == null)
        {
            return Task.CompletedTask;
        }

        character?.AddChildSafely(vfx);
        vfx.Position = Vector2.Zero;
        return Task.CompletedTask;
    }
}
