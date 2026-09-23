using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using ZoneTheSpire.Core.Infestation;

namespace ZoneTheSpire.Run.Wriggling;

/// <summary>
/// Wriggling N: a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when the
/// card is duplicated). When the card is played, its owner summons a Wriggler with N HP or adds N Max HP to their Wriggler.
/// X-cost cards multiply N by the energy spent.
/// </summary>
public sealed class WrigglingModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    private bool CostsX => Owner?.EnergyCost.CostsX ?? false;

    /// <summary>
    /// Adds Wriggling <paramref name="amount"/> to a playable card that doesn't already have it. Safe to call again for the
    /// same card (reward and shop hooks can be re-invoked). Returns whether it was added.
    /// </summary>
    internal static bool TryAdd(CardModel card, int amount)
    {
        if (card.TryGetModifier<WrigglingModifier>(out _) || card.Keywords.Contains(CardKeyword.Unplayable))
        {
            return false;
        }

        card.AddModifier<WrigglingModifier>(amount);
        return true;
    }

    /// <summary>Adds <paramref name="amount"/> to the card's Wriggling, giving it Wriggling first if it has none.</summary>
    internal static void AddOrIncrease(CardModel card, int amount)
    {
        if (card.TryGetModifier<WrigglingModifier>(out WrigglingModifier? existing))
        {
            existing.Amount += amount;
            return;
        }

        TryAdd(card, amount);
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = WrigglingRules.CardText(Amount * Shadow.ShadowCorruption.AmountMultiplier(Owner), CostsX);
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
            int amount = WrigglingRules.PlayAmount(Amount * Shadow.ShadowCorruption.AmountMultiplier(Owner), CostsX, cardPlay.Resources.EnergySpent);
            await WrigglerSummon.Summon(choiceContext, cardPlay.Player, amount);
        }
        catch (Exception ex)
        {
            Log.Warn($"Wriggling failed to summon a Wriggler: {ex}");
        }
    }
}
