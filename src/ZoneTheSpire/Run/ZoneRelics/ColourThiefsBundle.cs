using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.Prismatic;
using ZoneTheSpire.Run.Prismatic;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Colour Thief event relic: the first card from another character its owner plays each turn is played an extra time.
/// Counts earlier plays this turn from the combat history, like vanilla Echo Form, so every peer reaches the same answer.
/// An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class ColourThiefsBundle : ModArtRelicModel
{
    protected override string TextureName => "colour_thiefs_bundle";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "bag_of_marbles";

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner != Owner || Owner.Creature.CombatState is not { } combatState || !OtherCharacterCards.IsFromAnotherCharacter(card, Owner))
        {
            return playCount;
        }

        int playedThisTurn = CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
            entry.Actor == Owner.Creature
            && entry.CardPlay.IsFirstInSeries
            && entry.HappenedThisTurn(combatState)
            && OtherCharacterCards.IsFromAnotherCharacter(entry.CardPlay.Card, Owner));
        return PrismaticRules.BundleReplays(playedThisTurn) ? playCount + 1 : playCount;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        Flash();
        return Task.CompletedTask;
    }
}
