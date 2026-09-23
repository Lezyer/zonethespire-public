using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hallowed;

/// <summary>
/// Redemption (card modifier, playable non-attacks): when played, its owner loses up to 5 Hallowed. Given to the non-attack
/// cards of Blinding Hallows fight rewards (attacks get Hallowing instead). A permanent BaseLib CardModifier (saved, synced,
/// copied on duplication), a flag with no amount. Pays out once per play, not again on replays in a series.
/// </summary>
public sealed class RedemptionModifier : CardModifier, ILocalizationProvider
{
    public static string Line => HallowedText.CardLine(HallowedText.Redemption);

    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool Has(CardModel? card) => card != null && card.TryGetModifier<RedemptionModifier>(out _);

    /// <summary>Playable cards that aren't attacks (attacks get Hallowing), and never curses or statuses.</summary>
    internal static bool CanRedeem(CardModel card) =>
        card.Type is not (CardType.Attack or CardType.Curse or CardType.Status) && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>Removes Redemption from a card that has it. Returns whether it was removed.</summary>
    internal static bool TryRemove(CardModel card) =>
        card.TryGetModifier<RedemptionModifier>(out RedemptionModifier? modifier) && RemoveModifier(card, modifier);

    /// <summary>Adds Redemption to an eligible card that lacks it. Safe to call again. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanRedeem(card) || Has(card))
        {
            return false;
        }

        AddModifier<RedemptionModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        description = string.IsNullOrEmpty(description) ? Line : Line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
        if (!NestedTooltips.Enabled)
        {
            tips.Add(HoverTipFactory.FromPower<HallowedPower>());
        }
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (!cardPlay.IsFirstInSeries || cardPlay.Player?.Creature is not { IsDead: false } creature
                || creature.GetPower<HallowedPower>() is not { } hallowed)
            {
                return;
            }

            int removed = HallowedRules.RedemptionRemoval(hallowed.Amount);
            if (removed <= 0)
            {
                return;
            }

            await PowerCmd.ModifyAmount(choiceContext, hallowed, -removed, creature, cardPlay.Card);
        }
        catch (Exception ex)
        {
            Log.Warn($"Redemption failed on play: {ex}");
        }
    }
}
