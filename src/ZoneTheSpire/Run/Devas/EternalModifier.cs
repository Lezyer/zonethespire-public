using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// Makes one card Eternal, even when its kind normally isn't (the Doubt that comes with Worldly Attachment). A permanent card
/// modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when the card is duplicated), because the
/// game's own per-card keywords are not saved with a run. EternalKeywordPatch adds the keyword itself, so the card shows the
/// Eternal keyword and its vanilla tip, and card removal refuses it like any other Eternal card.
/// </summary>
public sealed class EternalModifier : CardModifier
{
    internal static bool IsEternal(CardModel? card) => card != null && card.TryGetModifier<EternalModifier>(out _);

    /// <summary>Makes the card Eternal if it isn't already.</summary>
    internal static void Apply(CardModel card)
    {
        if (!IsEternal(card))
        {
            card.AddModifier<EternalModifier>(1);
        }
    }

    /// <summary>The card's keywords with Eternal added (never the card's own set, which must not be changed here).</summary>
    internal static IReadOnlySet<CardKeyword> WithEternal(IReadOnlySet<CardKeyword> keywords)
    {
        if (keywords.Contains(CardKeyword.Eternal))
        {
            return keywords;
        }

        var withEternal = new HashSet<CardKeyword>(keywords) { CardKeyword.Eternal };
        return withEternal;
    }
}
