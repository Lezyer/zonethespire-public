using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.ForgottenEmpire;

/// <summary>
/// Marbling: a permanent card modifier for Forgotten Empire card rewards that don't gain Block. When played, up to 5 × the
/// card's cost (0-cost and X-cost cards count as 1) of its owner's Block turns into Marbled. Runs in the synced card play, so
/// every peer converts the same amount.
/// </summary>
public sealed class MarblingModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    private static int MarblingFor(CardModel card) =>
        ForgottenEmpireRules.MarblingAmount(card.EnergyCost.GetWithModifiers(CostModifiers.None), card.EnergyCost.CostsX);

    internal static bool CanAdd(CardModel card) =>
        ForgottenEmpireRules.RewardModifier(MarbleCards.GainsBlock(card), MarbleCards.IsUnplayable(card), MarbleCards.IsMarbled(card), MarbleCards.HasMarbling(card))
        == MarbleRewardModifier.Marbling;

    /// <summary>Adds Marbling if the card can have it. Safe to call again for the same card. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanAdd(card))
        {
            return false;
        }

        AddModifier<MarblingModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        if (Owner == null)
        {
            return;
        }

        string line = ForgottenEmpireRules.MarblingCardText(MarblingFor(Owner));
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
        // Explains Marbled itself (with nested tooltips, the Marbled link in the tip does).
        if (!NestedTooltips.Enabled)
        {
            tips.Add(new HoverTip(GetLoc("marbledTitle"), GetLoc("marbledDescription")));
        }
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            Creature? creature = cardPlay.Player?.Creature;
            if (Owner == null || creature == null || creature.IsDead)
            {
                return;
            }

            int converted = ForgottenEmpireRules.ConvertedBlock(creature.Block, MarblingFor(Owner));
            if (converted <= 0)
            {
                return;
            }

            creature.LoseBlockInternal(converted);
            await MarbledPower.Add(creature, converted, Owner);
        }
        catch (Exception ex)
        {
            Log.Warn($"Marbling failed on play: {ex}");
        }
    }
}
