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
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Deva's Domain Meditate (an extra rest site option; it uses the campfire action like Smith): choose an Attack or Skill. Without
/// Chakra it gains Chakra 2; with Chakra, its Chakra doubles. Then heal 15% of Max HP. Cancelling the card choice costs
/// nothing. Disabled when no card can take Chakra. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class MeditateRestSiteOption : RestSiteOption
{
    private readonly SmithRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _selection = new List<CardModel>();

    public MeditateRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new SmithRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.MeditateId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override bool IsEnabled => Owner.Deck.Cards.Any(ChakraModifier.CanHave);

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled)
        {
            return false;
        }

        var prefs = new CardSelectorPrefs(RestSiteLoc.MeditatePrompt, 1, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        _selection = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, ChakraModifier.CanHave)).ToList();
        if (_selection.Count == 0)
        {
            return false;
        }

        foreach (CardModel card in _selection)
        {
            ChakraModifier.Meditate(card);
        }

        int heal = KarmaRules.MeditateHeal(Owner.Creature.MaxHp);
        await CreatureCmd.Heal(Owner.Creature, heal);
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
