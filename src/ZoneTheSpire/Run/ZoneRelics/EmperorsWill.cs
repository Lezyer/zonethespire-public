using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ZoneTheSpire.Core.ForgottenEmpire;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Forgotten Emperor event relic. On pickup its owner removes a Power from their deck; the relic keeps that exact card
/// (upgrades, enchantment and card modifiers, as a saved SerializableCard like vanilla Pael's Tooth) and plays a copy of it for
/// free at the start of every combat, in the same auto-play phase vanilla History Course uses. The pickup choice goes through
/// the game's synchronized deck selector and the auto-play is part of the synchronized turn flow, so every peer agrees.
/// An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class EmperorsWill : ModArtRelicModel
{
    private const string StoredPowerKey = "StoredPower";

    private SerializableCard? _storedPower;

    protected override string TextureName => "emperors_will";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    protected override string IconBaseName => "royal_stamp";

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new StringVar(StoredPowerKey),
    };

    /// <summary>The Power this relic holds. Saved with the run.</summary>
    [SavedProperty]
    public SerializableCard? StoredPower
    {
        get => _storedPower;
        set
        {
            AssertMutable();
            _storedPower = value;
            UpdateDescription();
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (_storedPower == null)
            {
                return Array.Empty<IHoverTip>();
            }

            try
            {
                return new[] { HoverTipFactory.FromCard(CardModel.FromSerializable(_storedPower)) };
            }
            catch (Exception)
            {
                return Array.Empty<IHoverTip>();
            }
        }
    }

    /// <summary>Whether the card can be taken by Emperor's Will (and so whether the event's Bow option is available).</summary>
    internal static bool CanTake(CardModel card) =>
        ForgottenEmpireRules.CanBecomeWill(
            card.Type == CardType.Power,
            card.IsRemovable,
            card.Keywords.Contains(CardKeyword.Unplayable),
            card.EnergyCost.CostsX);

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _storedPower = null;
    }

    public override async Task AfterObtained()
    {
        try
        {
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = true,
            };
            CardModel? power = (await CardSelectCmd.FromDeckForRemoval(Owner, prefs, CanTake)).FirstOrDefault();
            if (power == null)
            {
                return;
            }

            StoredPower = ((CardModel)power.MutableClone()).ToSerializable();
            await CardPileCmd.RemoveFromDeck(power);
            Flash();
        }
        catch (Exception ex)
        {
            Log.Warn($"Emperor's Will failed on pickup: {ex}");
        }
    }

    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || _storedPower == null || Owner.PlayerCombatState?.TurnNumber != 1 || Owner.Creature.IsDead)
        {
            return;
        }

        try
        {
            if (Owner.Creature.CombatState is not CombatState combat)
            {
                return;
            }

            CardModel card = CardModel.FromSerializable(_storedPower);
            combat.AddCard(card, Owner);
            Flash();
            await CardCmd.AutoPlay(choiceContext, card.CreateDupe(Owner), null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Emperor's Will failed to play its Power: {ex}");
        }
    }

    private void UpdateDescription()
    {
        string title = string.Empty;
        if (_storedPower != null)
        {
            title = "\n[gold]" + SaveUtil.CardOrDeprecated(_storedPower.Id).Title + "[/gold]";
        }

        ((StringVar)DynamicVars[StoredPowerKey]).StringValue = title;
    }
}
