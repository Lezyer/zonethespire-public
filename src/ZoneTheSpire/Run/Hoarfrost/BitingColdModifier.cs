using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hoarfrost;

/// <summary>
/// Biting Cold N (card): a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when
/// the card is duplicated). Playing the card ices an enemy with N Biting Cold, which makes every attack against it deal that
/// much more damage until it thaws (see <see cref="BitingColdPower"/>). The cold lands once the card has finished, so it never
/// buffs the card's own attack, and it goes on whatever the card hit (see <see cref="BitingCold"/>). X-cost cards ice for N per
/// energy spent. Doubled on a Shadow Corrupted card, like Wriggling.
/// </summary>
public sealed class BitingColdModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    private bool CostsX => Owner?.EnergyCost.CostsX ?? false;

    /// <summary>The card's Biting Cold (doubled when Shadow Corrupted), or 0.</summary>
    internal static int AmountOf(CardModel? card) =>
        card != null && card.TryGetModifier<BitingColdModifier>(out BitingColdModifier? cold)
            ? cold.Amount * Shadow.ShadowCorruption.AmountMultiplier(card)
            : 0;

    /// <summary>Whether the card can carry Biting Cold: any playable card that isn't a Status or a Curse.</summary>
    internal static bool CanIce(CardModel card) =>
        card.Type is not (CardType.Status or CardType.Curse) && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>Gives an eligible card without Biting Cold that much. Safe to call again for the same card.</summary>
    internal static bool TryAdd(CardModel card, int amount)
    {
        if (amount <= 0 || !CanIce(card) || card.TryGetModifier<BitingColdModifier>(out _))
        {
            return false;
        }

        card.AddModifier<BitingColdModifier>(amount);
        return true;
    }

    /// <summary>Adds to the card's Biting Cold, giving it some first if it has none (Frostbind).</summary>
    internal static void AddOrIncrease(CardModel card, int amount)
    {
        if (card.TryGetModifier<BitingColdModifier>(out BitingColdModifier? existing))
        {
            existing.Amount += amount;
            return;
        }

        TryAdd(card, amount);
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = FrostRules.CardText(AmountOf(Owner), CostsX);
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
        if (!NestedTooltips.Enabled)
        {
            tips.Add(HoverTipFactory.FromPower<BitingColdPower>());
        }
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        int amount = FrostRules.PlayAmount(AmountOf(Owner), CostsX, cardPlay.Resources.EnergySpent);
        await BitingCold.Apply(choiceContext, cardPlay, amount);
    }
}
