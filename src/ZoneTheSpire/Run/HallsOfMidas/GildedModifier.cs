using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Midas;

namespace ZoneTheSpire.Run.HallsOfMidas;

/// <summary>
/// Gilded (The Golden Throne event): a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in
/// multiplayer, copied when the card is duplicated). When the card is played, its owner gains 5 gold, then loses 1 HP.
/// </summary>
public sealed class GildedModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool CanGild(CardModel card) =>
        HallsOfMidasRules.CanGild(card.Keywords.Contains(CardKeyword.Unplayable), card.TryGetModifier<GildedModifier>(out _));

    /// <summary>Makes the card Gilded if it can be. Safe to call again for the same card. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanGild(card))
        {
            return false;
        }

        AddModifier<GildedModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = HallsOfMidasRules.GildedCardTextFor(HallsOfMidasRules.GildedGold * Shadow.ShadowCorruption.AmountMultiplier(Owner));
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            Player? player = cardPlay.Player;
            Creature? creature = player?.Creature;
            if (player == null || creature == null || creature.IsDead)
            {
                return;
            }

            await PlayerCmd.GainGold(HallsOfMidasRules.GildedGold * Shadow.ShadowCorruption.AmountMultiplier(Owner), player);
            await CreatureCmd.Damage(choiceContext, creature, HallsOfMidasRules.GildedHpLoss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Gilded failed on play: {ex}");
        }
    }
}
