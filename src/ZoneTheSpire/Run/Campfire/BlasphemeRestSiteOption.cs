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
using ZoneTheSpire.Run.Hallowed;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Blinding Hallows Blaspheme (replaces Smith; SmithRestSiteOption is sealed, so this mirrors it like Festering Smith): upgrade
/// as many cards as the replaced Smith would (same upgrade screen, the Smith text with its {Count}, and the after-smith hook),
/// and every attack among them also gains Hallowing and Blasphemous. Cancelling costs nothing. Runs on every peer through the
/// synced rest site choice.
/// </summary>
public sealed class BlasphemeRestSiteOption : RestSiteOption
{
    private readonly SmithRestSiteOption _vanilla;
    private List<CardModel>? _selection;

    public BlasphemeRestSiteOption(Player owner, int smithCount)
        : base(owner)
    {
        _vanilla = new SmithRestSiteOption(owner) { SmithCount = smithCount };
        SmithCount = smithCount;
        RestSiteLoc.EnsureInjected();
    }

    /// <summary>How many cards are upgraded: the replaced Smith's count.</summary>
    public int SmithCount { get; }

    public override string OptionId => RestSiteLoc.BlasphemeId;

    public override IEnumerable<string> AssetPaths => _vanilla.AssetPaths;

    public override LocString Description
    {
        get
        {
            if (!IsEnabled)
            {
                return new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");
            }

            // The text starts with the vanilla Smith description, whose card count is the {Count} variable.
            var description = new LocString("rest_site_ui", "OPTION_" + OptionId + ".description");
            description.Add("Count", SmithCount);
            return description;
        }
    }

    public override bool IsEnabled => Owner.Deck.UpgradableCardCount != 0;

    public override async Task<bool> OnSelect()
    {
        var prefs = new CardSelectorPrefs(RestSiteLoc.BlasphemePrompt, SmithCount)
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
            // Only attacks are blasphemed here (Blasphemous itself fits any playable card).
            if (HallowingModifier.CanHallow(card))
            {
                HallowingModifier.TryAdd(card);
                BlasphemousModifier.TryAdd(card);
            }
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
        if (vfx != null)
        {
            character?.AddChildSafely(vfx);
            vfx.Position = Vector2.Zero;
        }

        return Task.CompletedTask;
    }
}
