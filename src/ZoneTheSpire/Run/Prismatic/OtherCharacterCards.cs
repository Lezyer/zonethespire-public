using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Prismatic;

namespace ZoneTheSpire.Run.Prismatic;

/// <summary>
/// "Cards from another character" for The Colour Thief event and its card and relic: cards in a character card pool other than
/// the player's own. Colorless, event, curse and other non-character cards never count.
/// </summary>
internal static class OtherCharacterCards
{
    public static bool IsFromAnotherCharacter(CardModel card, Player player)
    {
        CardPoolModel pool = card.Pool;
        return PrismaticRules.IsFromAnotherCharacter(
            ModelDb.AllCharacterCardPools.Contains(pool),
            pool == player.Character.CardPool);
    }

    /// <summary>Every unlocked card of every other character, in pool order (identical on every peer).</summary>
    public static IEnumerable<CardModel> Unlocked(Player player) =>
        ModelDb.AllCharacterCardPools
            .Where(pool => pool != player.Character.CardPool)
            .Distinct()
            .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint));

    /// <summary>Trade Colours replacements: other characters' cards that naturally cost 0.</summary>
    public static IEnumerable<CardModel> NaturallyFree(Player player) =>
        Unlocked(player).Where(card => PrismaticRules.IsTradeReplacement(
            card.EnergyCost.Canonical,
            card.EnergyCost.CostsX,
            card.CanonicalKeywords.Contains(CardKeyword.Unplayable)));
}
