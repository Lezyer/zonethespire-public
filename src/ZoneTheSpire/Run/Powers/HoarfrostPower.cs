using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Run.Hoarfrost;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Hoarfrost (every player, whole fight): 2 cards in the owner's hand freeze at the start of each of their turns and can't be
/// played, and everything thaws when the enemies' turn begins. The freezing itself happens just before the turn-start draw
/// (HoarfrostDrawPatch), so the cards arrive iced, and topped up right after the draw when fewer arrived frozen (reshuffle
/// turns); this power holds the count, marks who the zone applies to, and thaws the hand
/// again. A buff, so Artifact never blocks it and nothing that clears debuffs removes it. Its icon is served by
/// BloodDrinkerIconPatch.
/// </summary>
public sealed class HoarfrostPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<FrailPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<FrailPower>().ResolvedBigIconPath;

    /// <summary>Whether this player's hand freezes each turn.</summary>
    public static bool IsOn(Player player) => player.Creature is { } creature && creature.HasPower<HoarfrostPower>();

    /// <summary>
    /// A Frozen card can't be played, by hand or by anything that plays cards for you. The block lives on this power rather than
    /// on the run modifier so the game can name what stopped you ("blocked by Hoarfrost"): it only knows how to name a card,
    /// relic, power, enchantment or affliction, and anything else shows as &lt;UNKNOWN&gt;.
    /// </summary>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner?.Creature != Owner || !FrozenCards.IsFrozen(card);

    /// <summary>Right after the turn-start draw: top the hand up to its frozen cards when fewer arrived frozen (reshuffle turns).</summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player)
        {
            FrozenCards.FreezeAfterHandDraw(player);
        }

        return Task.CompletedTask;
    }

    /// <summary>The enemies' turn begins: everything thaws, so frost never builds up from turn to turn.</summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy && Owner.Player is { } player)
        {
            FrozenCards.ThawAll(player);
        }

        return Task.CompletedTask;
    }
}
