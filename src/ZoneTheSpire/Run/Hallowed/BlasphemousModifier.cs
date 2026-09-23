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
/// Blasphemous (card modifier, any playable card): when played, its owner gains 5 Hallowed. Permanent (saved, synced, copied
/// on duplication), added by Blaspheme (attacks), the Sacrosanct Flail (attacks) and the Sacred Tribunal's Deny (any type).
/// A flag: Blasphemer's hand mark never grants on top of it.
/// </summary>
public sealed class BlasphemousModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool Has(CardModel? card) => card != null && card.TryGetModifier<BlasphemousModifier>(out _);

    /// <summary>Any card that can be played: never curses or statuses.</summary>
    internal static bool CanBlaspheme(CardModel card) =>
        card.Type is not (CardType.Curse or CardType.Status) && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>Removes Blasphemous from a card that has it. Returns whether it was removed.</summary>
    internal static bool TryRemove(CardModel card) =>
        card.TryGetModifier<BlasphemousModifier>(out BlasphemousModifier? modifier) && RemoveModifier(card, modifier);

    internal static bool TryAdd(CardModel card)
    {
        if (!CanBlaspheme(card) || Has(card))
        {
            return false;
        }

        AddModifier<BlasphemousModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        description = string.IsNullOrEmpty(description) ? BlasphemousMark.Line : BlasphemousMark.Line + "\n" + description;
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
            if (!cardPlay.IsFirstInSeries || cardPlay.Player?.Creature is not { IsDead: false } creature)
            {
                return;
            }

            await PowerCmd.Apply<HallowedPower>(choiceContext, creature, HallowedRules.HallowedPerBlasphemy, creature, cardPlay.Card);
        }
        catch (Exception ex)
        {
            Log.Warn($"Blasphemous failed on play: {ex}");
        }
    }
}
