using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hallowed;

/// <summary>
/// Blasphemous marks (Blasphemer). A mark is a state of the hand for one turn, never something a card carries:
/// <list type="bullet">
/// <item>2 cards are marked once per turn, right after the turn-start draw (so reshuffle turns mark from the full hand).</item>
/// <item>Cards without the permanent Blasphemous modifier are marked first; one with it only when nothing else can be.</item>
/// <item>A marked card that leaves the hand (or the Play pile, while it resolves) reads as normal again.</item>
/// <item>The marks clear when the enemy turn begins.</item>
/// </list>
/// Playing a marked card grants 5 Hallowed once (first play of a series), unless the permanent modifier grants it instead.
/// </summary>
internal static class BlasphemousMark
{
    public static string Line => HallowedText.CardLine(HallowedText.Blasphemous);

    private static readonly ConditionalWeakTable<CardModel, StrongBox<(PlayerCombatState Combat, int Turn)>> Marks = new();
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> MarkedTurn = new();

    public static bool IsMarked(CardModel? card)
    {
        if (card == null || !Marks.TryGetValue(card, out StrongBox<(PlayerCombatState Combat, int Turn)>? mark))
        {
            return false;
        }

        return card.Pile?.Type is PileType.Hand or PileType.Play
            && card.Owner?.PlayerCombatState is { } combat
            && ReferenceEquals(combat, mark.Value.Combat)
            && combat.TurnNumber == mark.Value.Turn;
    }

    /// <summary>Called from BlasphemerPower.AfterPlayerTurnStart, which the game fires right after the hand draw.</summary>
    public static void MarkAfterHandDraw(Player player)
    {
        try
        {
            if (!BlasphemerPower.IsOn(player) || player.PlayerCombatState is not { } combat)
            {
                return;
            }

            if (MarkedTurn.TryGetValue(combat, out StrongBox<int>? last) && last.Value == combat.TurnNumber)
            {
                return;
            }

            MarkedTurn.GetValue(combat, static _ => new StrongBox<int>(0)).Value = combat.TurnNumber;
            List<CardModel> hand = combat.Hand.Cards.ToList();
            IRunState runState = player.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            IReadOnlyList<int> picks = HallowedRules.PickPreferring(
                runState.Rng.Seed,
                HallowedRules.BlasphemousStream(location, combat.TurnNumber),
                player.NetId,
                hand.Select(card => !BlasphemousModifier.Has(card)).ToList(),
                HallowedRules.BlasphemousPerTurn);
            foreach (int index in picks)
            {
                Marks.AddOrUpdate(hand[index], new StrongBox<(PlayerCombatState, int)>((combat, combat.TurnNumber)));
                Refresh(hand[index]);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Blasphemer failed to mark a hand: {ex}");
        }
    }

    /// <summary>The enemy turn begins: every mark of this player clears.</summary>
    public static void Clear(Player player)
    {
        try
        {
            if (player.PlayerCombatState is not { } combat)
            {
                return;
            }

            foreach (CardModel card in combat.AllPiles.SelectMany(pile => pile.Cards).ToList())
            {
                if (Marks.TryGetValue(card, out _))
                {
                    Marks.Remove(card);
                    Refresh(card);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Blasphemer failed to clear marks: {ex}");
        }
    }

    /// <summary>
    /// Dispatched from ZoneTheSpireModifier.AfterCardPlayed. The card is still in the Play pile there. Replays in a series
    /// don't grant again, and the permanent modifier's own grant replaces the mark's.
    /// </summary>
    public static async Task OnCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            CardModel card = cardPlay.Card;
            if (!cardPlay.IsFirstInSeries || !IsMarked(card) || BlasphemousModifier.Has(card)
                || cardPlay.Player?.Creature is not { IsDead: false } creature)
            {
                return;
            }

            await PowerCmd.Apply<HallowedPower>(choiceContext, creature, HallowedRules.HallowedPerBlasphemy, creature, card);
        }
        catch (Exception ex)
        {
            Log.Warn($"Blasphemous mark failed on play: {ex}");
        }
    }

    /// <summary>Redraws the card so its line and overlay show or clear straight away (FrozenCards' refresh).</summary>
    private static void Refresh(CardModel card)
    {
        try
        {
            NCard.FindOnTable(card)?.UpdateVisuals(card.Pile?.Type ?? PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh a Blasphemous card: {ex.Message}");
        }
    }
}
