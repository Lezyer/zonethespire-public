using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Blood Rain Blood Sacrifice: upgrade a card, lose 10 Max HP, then heal to full. The upgrade is selected first so cancelling
/// leaves the player untouched; a fully upgraded deck can still use the sacrifice. It is neither a Rest nor a Smith, so those
/// hooks and rewards don't trigger. Runs on every peer through the synchronized rest-site choice and deck selector.
/// </summary>
public sealed class BloodSacrificeRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _upgradedCards = new List<CardModel>();

    public BloodSacrificeRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.BloodSacrificeId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override bool IsEnabled => BloodRainRules.CanSacrifice(Owner.Creature.MaxHp);

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        if (!BloodRainRules.CanSacrifice(Owner.Creature.MaxHp))
        {
            return false;
        }

        if (Owner.Deck.UpgradableCardCount > 0)
        {
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            _upgradedCards = (await CardSelectCmd.FromDeckForUpgrade(Owner, prefs)).ToList();
            if (_upgradedCards.Count == 0)
            {
                return false;
            }
        }

        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, BloodRainRules.SacrificeMaxHpCost, isFromCard: false);
        int missing = Owner.Creature.MaxHp - Owner.Creature.CurrentHp;
        if (missing > 0)
        {
            await CreatureCmd.Heal(Owner.Creature, missing);
        }

        foreach (CardModel card in _upgradedCards)
        {
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }

        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        HealRestSiteOption.PlayRestSiteHealSfx();
        if (_upgradedCards.Count > 0)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardSmithVfx.Create(_upgradedCards.ToArray()));
        }

        return Task.CompletedTask;
    }

    public override Task DoRemotePostSelectVfx()
    {
        if (_upgradedCards.Count == 0)
        {
            return Task.CompletedTask;
        }

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
