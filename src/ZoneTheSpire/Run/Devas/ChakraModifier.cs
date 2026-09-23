using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Devas;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// Chakra N (Deva's Domain): a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied
/// when the card is duplicated). The card starts every combat, in any zone, with Karma equal to its Chakra (see
/// <see cref="Karma"/>). Doubled on a Shadow Corrupted card, like Wriggling.
/// </summary>
public sealed class ChakraModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    /// <summary>Whether the card can be given Chakra: an Attack or Skill that can be played (card rewards and Meditate).</summary>
    internal static bool CanHave(CardModel card) =>
        card.Type is CardType.Attack or CardType.Skill && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>
    /// Whether the card can be given Chakra at all: any playable card that isn't a Status or a Curse, Powers included. Used by
    /// the Troubled Pilgrim, which is less picky than card rewards and Meditate.
    /// </summary>
    internal static bool CanHaveAny(CardModel card) =>
        card.Type is not (CardType.Status or CardType.Curse) && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>The card's Chakra (doubled when Shadow Corrupted), or 0.</summary>
    internal static int AmountOf(CardModel? card) =>
        card != null && card.TryGetModifier<ChakraModifier>(out ChakraModifier? chakra)
            ? chakra.Amount * Shadow.ShadowCorruption.AmountMultiplier(card)
            : 0;

    /// <summary>Gives an eligible card without Chakra Chakra <paramref name="amount"/>. Safe to call again for the same card.</summary>
    internal static bool TryAdd(CardModel card, int amount)
    {
        if (amount <= 0 || !CanHave(card) || card.TryGetModifier<ChakraModifier>(out _))
        {
            return false;
        }

        card.AddModifier<ChakraModifier>(amount);
        return true;
    }

    /// <summary>Adds <paramref name="amount"/> to the card's Chakra, giving it Chakra first if it has none.</summary>
    internal static void AddOrIncrease(CardModel card, int amount)
    {
        if (card.TryGetModifier<ChakraModifier>(out ChakraModifier? existing))
        {
            existing.Amount += amount;
            return;
        }

        if (amount > 0 && CanHaveAny(card))
        {
            card.AddModifier<ChakraModifier>(amount);
        }
    }

    /// <summary>Removes the card's Chakra, and says how much it had (0 when it had none).</summary>
    internal static int Remove(CardModel card)
    {
        if (!card.TryGetModifier<ChakraModifier>(out ChakraModifier? chakra))
        {
            return 0;
        }

        int amount = chakra.Amount;
        RemoveModifier(card, chakra);
        return amount;
    }

    /// <summary>Meditate: Chakra 2 for a card without Chakra, double for a card with it.</summary>
    internal static void Meditate(CardModel card)
    {
        if (card.TryGetModifier<ChakraModifier>(out ChakraModifier? existing))
        {
            existing.Amount = KarmaRules.MeditatedChakra(existing.Amount);
            return;
        }

        TryAdd(card, KarmaRules.MeditatedChakra(0));
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = KarmaRules.ChakraText(AmountOf(Owner));
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
    }
}
