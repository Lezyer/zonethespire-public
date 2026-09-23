using System.Linq;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ZoneTheSpire.Run.ForgottenEmpire;

/// <summary>Card checks shared by the Forgotten Empire modifiers, shop and Sculpt.</summary>
internal static class MarbleCards
{
    /// <summary>Whether the card has a Block value Sculpt can raise.</summary>
    public static bool HasBlockVar(CardModel card) => card.DynamicVars.Values.OfType<BlockVar>().Any();

    /// <summary>Whether the card gains Block (flagged by the game, or has a Block value).</summary>
    public static bool GainsBlock(CardModel card) => card.GainsBlock || HasBlockVar(card);

    public static bool IsUnplayable(CardModel card) => card.Keywords.Contains(CardKeyword.Unplayable);

    public static bool IsMarbled(CardModel? card) => card != null && card.TryGetModifier<MarbledModifier>(out _);

    public static bool HasMarbling(CardModel card) => card.TryGetModifier<MarblingModifier>(out _);
}
