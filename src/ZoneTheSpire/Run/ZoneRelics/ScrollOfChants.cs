using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.ZoneRelics;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Deva's Domain zone relic. On the first player turn, once the opening hand is drawn, 3 of its cards gain Karma 2 (so they cost
/// that much less, and playing one spends only the discount it used). Cards that cost something are chosen first; a 0-cost card
/// is only picked when nothing else in hand can take it. The picks come from the run's synchronized CombatCardSelection stream,
/// so every multiplayer peer chants over the same cards. An Event relic, so it only comes from Deva's Domain chests.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class ScrollOfChants : ModArtRelicModel
{
    protected override string TextureName => "scroll_of_chants";

    protected override string IconBaseName => "arcane_scroll";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { Karma.Tip };

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        try
        {
            if (side != CombatSide.Player
                || Owner.PlayerCombatState?.TurnNumber != 1
                || !participants.Contains(Owner.Creature))
            {
                return Task.CompletedTask;
            }

            List<CardModel> hand = Owner.PlayerCombatState.Hand.Cards.ToList();
            List<CardModel> costing = hand
                .Where(card => card.EnergyCost.CostsX || card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
                .ToList();
            List<CardModel> free = hand.Where(card => !costing.Contains(card)).ToList();

            var chanted = new List<string>();
            for (int i = 0; i < ZoneRelicEffects.ScrollOfChantsCards; i++)
            {
                // 0-cost cards, which have no cost for the Karma to lower, are only reached for once nothing else is left.
                List<CardModel> choices = costing.Count > 0 ? costing : free;
                if (choices.Count == 0)
                {
                    break;
                }

                CardModel card = combatState.RunState.Rng.CombatCardSelection.NextItem(choices)!;
                choices.Remove(card);
                Karma.Add(card, ZoneRelicEffects.ScrollOfChantsKarma);
                chanted.Add(card.Id.Entry);
            }

            if (chanted.Count > 0)
            {
                Flash();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Scroll of Chants failed: {ex}");
        }

        return Task.CompletedTask;
    }
}
