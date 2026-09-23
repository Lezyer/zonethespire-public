using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hoarfrost;

/// <summary>
/// Frozen cards (Hoarfrost fights). Frost is a state of a player's hand for one turn, never something a card carries around:
/// <list type="bullet">
/// <item>2 cards freeze once per turn, picked just before the turn-start draw so they arrive already iced; right after the draw
/// the hand is topped up to 2 frozen cards when fewer arrived frozen (reshuffle turns, a full hand).</item>
/// <item>They thaw when the enemies' turn begins, so frost can never build up from turn to turn.</item>
/// <item>A frozen card that leaves the hand is no longer frozen, so nothing frozen ever comes back out of the draw pile.</item>
/// <item>Only playing is blocked (HoarfrostPower.ShouldPlay), auto-plays included; discard, exhaust and retain all work.</item>
/// </list>
/// The pick is seeded from the run seed, node, player and turn, so every peer freezes the same cards. The ice itself is local
/// rendering (CardModifierVisuals).
/// </summary>
internal static class FrozenCards
{
    /// <summary>When a card was frozen: the hand it was frozen in and that player's turn number.</summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<(PlayerCombatState Combat, int Turn)>> Frost = new();

    /// <summary>Players whose hand was already frozen this turn (frost is picked once a turn, never topped up).</summary>
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> FrozenTurn = new();

    /// <summary>
    /// Whether the card is frozen right now: frozen this turn, in this combat, and still in its owner's hand. A card that left
    /// the hand reads as thawed, so a stale mark can never travel with it.
    /// </summary>
    public static bool IsFrozen(CardModel? card)
    {
        if (card == null || !Frost.TryGetValue(card, out StrongBox<(PlayerCombatState Combat, int Turn)>? frost))
        {
            return false;
        }

        return card.Pile?.Type == PileType.Hand
            && card.Owner?.PlayerCombatState is { } combat
            && ReferenceEquals(combat, frost.Value.Combat)
            && combat.TurnNumber == frost.Value.Turn;
    }

    /// <summary>
    /// Freezes this turn's cards, just before the turn-start draw: the hand the player is about to draw into, plus the cards on
    /// their way in, are all candidates. Does nothing outside a Hoarfrost fight, or when this player's hand froze already.
    /// </summary>
    public static void FreezeBeforeHandDraw(Player player, int drawing)
    {
        try
        {
            if (player.Creature?.HasPower<HoarfrostPower>() != true || player.PlayerCombatState is not { } combat)
            {
                return;
            }

            if (FrozenTurn.TryGetValue(combat, out StrongBox<int>? last) && last.Value == combat.TurnNumber)
            {
                return;
            }

            FrozenTurn.GetValue(combat, static _ => new StrongBox<int>(0)).Value = combat.TurnNumber;

            // The cards about to be drawn come first: freezing those means they arrive iced instead of icing over in hand.
            List<CardModel> candidates = combat.DrawPile.Cards.Take(Math.Max(0, drawing)).Concat(combat.Hand.Cards).ToList();
            int count = FrostRules.FreezeCountFor(candidates.Count);
            if (count <= 0)
            {
                return;
            }

            IRunState runState = player.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            foreach (int index in FrostRules.PickFrozenIndices(runState.Rng.Seed, location, player.NetId, combat.TurnNumber, candidates.Count, count))
            {
                Freeze(candidates[index], combat);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Hoarfrost failed to freeze a hand: {ex}");
        }
    }

    /// <summary>Players whose hand was already topped up after this turn's draw.</summary>
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> ToppedUpTurn = new();

    /// <summary>
    /// Right after the turn-start draw (HoarfrostPower.AfterPlayerTurnStart): freezes more cards in hand when fewer than 2 are
    /// frozen there. The pick before the draw only sees the cards left in the draw pile, so on a turn where the discard pile is
    /// reshuffled into it (or the hand was full, or the draw was cut short) fewer cards arrive frozen; this tops the hand up
    /// from the cards actually drawn. Once per turn, seeded like the first pick, so every peer freezes the same cards.
    /// </summary>
    public static void FreezeAfterHandDraw(Player player)
    {
        try
        {
            if (player.Creature?.HasPower<HoarfrostPower>() != true || player.PlayerCombatState is not { } combat)
            {
                return;
            }

            if (ToppedUpTurn.TryGetValue(combat, out StrongBox<int>? last) && last.Value == combat.TurnNumber)
            {
                return;
            }

            ToppedUpTurn.GetValue(combat, static _ => new StrongBox<int>(0)).Value = combat.TurnNumber;
            List<CardModel> hand = combat.Hand.Cards.ToList();
            int missing = FrostRules.FrozenPerTurn - hand.Count(IsFrozen);
            List<CardModel> candidates = hand.Where(card => !IsFrozen(card)).ToList();
            int count = Math.Min(Math.Max(0, missing), candidates.Count);
            if (count <= 0)
            {
                return;
            }

            IRunState runState = player.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            foreach (int index in FrostRules.PickFrozenIndices(runState.Rng.Seed, location, player.NetId, combat.TurnNumber, candidates.Count, count))
            {
                Freeze(candidates[index], combat);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Hoarfrost failed to top up a hand after the draw: {ex}");
        }
    }

    /// <summary>Thaws everything this player has frozen (the enemies' turn beginning). Frost never carries into another turn.</summary>
    public static void ThawAll(Player player)
    {
        try
        {
            if (player.PlayerCombatState is not { } combat)
            {
                return;
            }

            foreach (CardModel card in combat.AllPiles.SelectMany(pile => pile.Cards).ToList())
            {
                if (Frost.TryGetValue(card, out _))
                {
                    Frost.Remove(card);
                    Refresh(card);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Hoarfrost failed to thaw a hand: {ex}");
        }
    }

    private static void Freeze(CardModel card, PlayerCombatState combat)
    {
        Frost.AddOrUpdate(card, new StrongBox<(PlayerCombatState, int)>((combat, combat.TurnNumber)));
        Refresh(card);
    }

    /// <summary>Redraws the card wherever it is on screen, so the ice shows or clears straight away.</summary>
    private static void Refresh(CardModel card)
    {
        try
        {
            NCard.FindOnTable(card)?.UpdateVisuals(card.Pile?.Type ?? PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh a frozen card: {ex.Message}");
        }
    }
}
