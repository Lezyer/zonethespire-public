using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hoarfrost;

/// <summary>
/// Who a card's Biting Cold ices. The cold lands after the card has finished (BaseLib runs card modifiers' OnPlay after the card
/// played), so a card never ices the enemy in time to buff its own attack:
/// <list type="bullet">
/// <item>An attack ices every enemy it damaged, however many that was.</item>
/// <item>A card that damaged nobody but was aimed at an enemy ices that enemy.</item>
/// <item>Anything else (Powers, self- or ally-targeted cards) ices one random living enemy, picked from the run seed, node,
/// player and play, so every peer ices the same one.</item>
/// </list>
/// </summary>
internal static class BitingCold
{
    /// <summary>The enemies a card damaged during the play in progress.</summary>
    private static readonly ConditionalWeakTable<CardModel, List<Creature>> Damaged = new();

    /// <summary>Cards played per combat, per player: it tells otherwise identical plays apart when picking a random enemy.</summary>
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> Plays = new();

    /// <summary>A new play of this card starts with nothing damaged yet, and counts towards the player's plays this combat.</summary>
    public static void BeforeCardPlayed(CardPlay cardPlay)
    {
        Damaged.Remove(cardPlay.Card);
        if (cardPlay.PlayIndex == 0 && cardPlay.Player?.PlayerCombatState is { } combat)
        {
            Plays.GetValue(combat, static _ => new StrongBox<int>(0)).Value++;
        }
    }

    /// <summary>Remembers an enemy a card's attack damaged, so the card's Biting Cold can ice everything it hit.</summary>
    public static void AfterDamageGiven(Creature target, ValueProp props, CardModel? cardSource)
    {
        if (cardSource == null || !props.IsPoweredAttack() || target.Side != CombatSide.Enemy || !target.IsAlive)
        {
            return;
        }

        List<Creature> damaged = Damaged.GetValue(cardSource, static _ => new List<Creature>());
        if (!damaged.Contains(target))
        {
            damaged.Add(target);
        }
    }

    /// <summary>Ices what this play of the card should ice, with <paramref name="amount"/> Biting Cold each.</summary>
    public static async Task Apply(PlayerChoiceContext choiceContext, CardPlay cardPlay, int amount)
    {
        try
        {
            if (amount <= 0 || cardPlay.Player is not { } player || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            IReadOnlyList<Creature> targets = TargetsOf(cardPlay, player);
            foreach (Creature target in targets)
            {
                await PowerCmd.Apply<BitingColdPower>(choiceContext, target, amount, player.Creature, cardPlay.Card);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Biting Cold failed to ice an enemy: {ex}");
        }
    }

    private static IReadOnlyList<Creature> TargetsOf(CardPlay cardPlay, Player player)
    {
        List<Creature> hit = Damaged.TryGetValue(cardPlay.Card, out List<Creature>? damaged)
            ? damaged.Where(enemy => enemy.IsAlive).ToList()
            : new List<Creature>();
        if (hit.Count > 0)
        {
            return hit;
        }

        if (cardPlay.Target is { Side: CombatSide.Enemy, IsAlive: true } aimed)
        {
            return new[] { aimed };
        }

        List<Creature> living = player.Creature.CombatState?.Enemies.Where(enemy => enemy.IsAlive).ToList() ?? new List<Creature>();
        IRunState runState = player.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        int plays = player.PlayerCombatState is { } combat && Plays.TryGetValue(combat, out StrongBox<int>? count) ? count.Value : 0;
        string playKey = cardPlay.Card.Id.Entry + ":" + plays;
        int index = FrostRules.PickIcedEnemyIndex(runState.Rng.Seed, location, player.NetId, playKey, living.Count);
        return index < 0 ? Array.Empty<Creature>() : new[] { living[index] };
    }
}
