using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ZoneRelics;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Phantasmal Tombs zone relic. On the first player turn, after the opening hand is drawn, it chooses a positive-cost card
/// when possible (falling back to a zero-cost card), and makes that combat copy free, Retained, and heal 3 HP whenever it is
/// played for the rest of combat. CombatCardSelection is saved and replayed on every peer, so every peer picks the same card.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class FlutteringPhantasm : ModArtRelicModel
{
    protected override string TextureName => "fluttering_phantasm";

    protected override string IconBaseName => "bird_faced_urn";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        try
        {
            if (side != CombatSide.Player || Owner.PlayerCombatState?.TurnNumber != 1 || !participants.Contains(Owner.Creature))
            {
                return Task.CompletedTask;
            }

            List<CardModel> eligible = Owner.PlayerCombatState.Hand.Cards
                .Where(card => !card.EnergyCost.CostsX && !card.Keywords.Contains(CardKeyword.Unplayable))
                .ToList();
            List<CardModel> preferred = eligible
                .Where(card => card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
                .ToList();
            List<CardModel> choices = preferred.Count > 0 ? preferred : eligible;
            if (choices.Count == 0)
            {
                return Task.CompletedTask;
            }

            CardModel card = combatState.RunState.Rng.CombatCardSelection.NextItem(choices)!;
            card.EnergyCost.SetThisCombat(0);
            card.AddKeyword(CardKeyword.Retain);
            CardModifier.AddModifier<FlutteringPhantasmModifier>(card);
            Flash();
        }
        catch (Exception ex)
        {
            Log.Warn($"Fluttering Phantasm failed: {ex}");
        }

        return Task.CompletedTask;
    }
}
