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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Run.Hoarfrost;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Hoarfrost Frostbind (an extra rest site option; it uses the campfire action like Smith): choose a card, and it gains Biting
/// Cold 4 per energy of its cost, on top of any it already has. Cancelling costs nothing. Disabled when no card can carry Biting
/// Cold. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class FrostbindRestSiteOption : RestSiteOption
{
    private readonly SmithRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _selection = new List<CardModel>();

    public FrostbindRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new SmithRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.FrostbindId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override bool IsEnabled => Owner.Deck.Cards.Any(BitingColdModifier.CanIce);

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled)
        {
            return false;
        }

        var prefs = new CardSelectorPrefs(RestSiteLoc.FrostbindPrompt, 1, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        _selection = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, BitingColdModifier.CanIce)).ToList();
        if (_selection.Count == 0)
        {
            return false;
        }

        foreach (CardModel card in _selection)
        {
            BitingColdModifier.AddOrIncrease(card, FrostRules.FrostbindCold(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }

        return true;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardSmithVfx.Create(_selection.ToArray()));
        await Cmd.CustomScaledWait(1f, 2f, ignoreCombatEnd: false, ct);
    }

    public override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? character = NRestSiteRoom.Instance?.Characters.FirstOrDefault(c => c.Player == Owner);
        NCardSmithVfx? vfx = NCardSmithVfx.Create();
        if (vfx != null)
        {
            character?.AddChildSafely(vfx);
            vfx.Position = Vector2.Zero;
        }

        return Task.CompletedTask;
    }
}
