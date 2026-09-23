using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Prismatic Storm zone relic. After the opening hand is drawn, creates two distinct combat cards from every character pool
/// plus colourless and makes them free for the first turn. CardFactory and CombatCardGeneration use the game's synchronized,
/// saved combat generation path, so all peers create the same cards in the same order.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class PrismCloud : ModArtRelicModel
{
    protected override string TextureName => "prism_cloud";

    protected override string IconBaseName => "prismatic_shard";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        try
        {
            if (side != CombatSide.Player || Owner.PlayerCombatState?.TurnNumber != 1 || !participants.Contains(Owner.Creature))
            {
                return;
            }

            IEnumerable<CardModel> pool = ModelDb.AllCharacterCardPools
                .Append(ModelDb.CardPool<ColorlessCardPool>())
                .Distinct()
                .SelectMany(cardPool => cardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint));
            List<CardModel> cards = CardFactory.GetDistinctForCombat(
                Owner,
                pool,
                ZoneRelicEffects.PrismCloudCardCount,
                combatState.RunState.Rng.CombatCardGeneration).ToList();
            foreach (CardModel card in cards)
            {
                card.EnergyCost.SetThisTurnOrUntilPlayed(0);
            }

            if (cards.Count > 0)
            {
                Flash();
                await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Prism Cloud failed to generate cards: {ex}");
        }
    }
}
