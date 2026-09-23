using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// Shaded cards: a card whose face is hidden (only its cost and card type show) until it is played. In Shadow Corruption
/// fights one more card in each player's hand is shaded at the start of each of their turns, and one more (in every fight) for
/// the holder of the Last-Light Lantern (picked just before the hand draw, so a card is never seen face up on its way into the
/// hand); shop cards can be shaded too. The flag lives on the card instance (combat cards are copies, so fights never shade deck
/// cards), and the picks use the run seed, map location, player and turn, so every peer shades the same cards. The hidden face
/// is local presentation only.
/// </summary>
internal static class ShadedCards
{
    private static readonly ConditionalWeakTable<CardModel, object> Shaded = new();

    public static bool IsShaded(CardModel? card) => card != null && Shaded.TryGetValue(card, out _);

    public static void Shade(CardModel card)
    {
        Shaded.AddOrUpdate(card, card);
        Refresh(card);
    }

    public static void Unshade(CardModel card)
    {
        if (Shaded.Remove(card))
        {
            Refresh(card);
        }
    }

    /// <summary>Light the Way: every shaded card in the player's hand is revealed (it stays revealed until shaded again).</summary>
    public static void UnshadeHand(Player player)
    {
        if (player.PlayerCombatState is not { } combat)
        {
            return;
        }

        foreach (CardModel card in combat.Hand.Cards.Where(IsShaded).ToList())
        {
            Unshade(card);
        }
    }

    /// <summary>Whether this card may be shaded: never statuses or curses, never twice.</summary>
    public static bool CanShade(CardModel card) =>
        ShadowRules.CanShade(card.Type is CardType.Status or CardType.Curse, IsShaded(card));

    /// <summary>
    /// Start of turn, just before the hand draw: shades this turn's cards among the player's hand and the cards this draw will
    /// take off the top of the draw pile, so a card on its way into the hand is already shaded when it appears. One card in a
    /// Shadow Corruption fight, one for holding the Last-Light Lantern (two when both apply). Runs in the synced turn-start
    /// flow with the same piles on every peer, so every peer shades the same cards.
    /// </summary>
    public static void ShadeBeforeHandDraw(Player player, decimal count)
    {
        try
        {
            if (player.PlayerCombatState is not { } combat || ShadesWanted(player) is var wanted && wanted <= 0)
            {
                return;
            }

            int draws = (int)Math.Ceiling(Math.Max(0m, count));
            draws = Math.Min(draws, Math.Max(0, CardPile.MaxCardsInHand - combat.Hand.Cards.Count));
            ShadeUpTo(player, combat, combat.Hand.Cards.Concat(combat.DrawPile.Cards.Take(draws)).ToList(), wanted);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shade cards before the hand draw: {ex}");
        }
    }

    /// <summary>
    /// After the turn-start draw: shades the rest of this turn's cards in the player's hand when fewer could be shaded before
    /// the draw (e.g. eligible cards came from a reshuffle). Stops when every eligible card is already shaded.
    /// </summary>
    public static void ShadeInHand(Player player)
    {
        try
        {
            if (player.PlayerCombatState is not { } combat)
            {
                return;
            }

            ShadeUpTo(player, combat, combat.Hand.Cards.ToList(), ShadesWanted(player));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shade a card: {ex}");
        }
    }

    /// <summary>How many cards this player shades each turn in this fight.</summary>
    private static int ShadesWanted(Player player) => ShadowRules.ShadesPerTurn(
        ZoneEffectQuery.IsActive(player.RunState, ShadowCorruptionBiome.ShadowFightsEffectId),
        LastLightLantern.IsHeldBy(player));

    private static readonly ConditionalWeakTable<Player, StrongBox<(PlayerCombatState Combat, int Turn, int Count)>> ShadedTurn = new();

    private static int ShadedThisTurn(Player player, PlayerCombatState combat) =>
        ShadedTurn.TryGetValue(player, out var last) && ReferenceEquals(last.Value.Combat, combat) && last.Value.Turn == combat.TurnNumber
            ? last.Value.Count
            : 0;

    private static void ShadeUpTo(Player player, PlayerCombatState combat, IReadOnlyList<CardModel> cards, int wanted)
    {
        int done = ShadedThisTurn(player, combat);
        MapCoord? coord = player.RunState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(player.RunState.CurrentActIndex, coord?.row, coord?.col, 0);
        while (done < wanted)
        {
            List<CardModel> candidates = cards.Where(CanShade).ToList();
            int index = ShadowRules.PickShadeIndex(player.RunState.Rng.Seed, location, player.NetId, combat.TurnNumber, candidates.Count, done);
            if (index < 0)
            {
                return;
            }

            Shade(candidates[index]);
            done++;
            ShadedTurn.AddOrUpdate(player, new StrongBox<(PlayerCombatState Combat, int Turn, int Count)>((combat, combat.TurnNumber, done)));
        }
    }

    /// <summary>Redraws the card wherever it is on screen, so the shade shows or clears straight away.</summary>
    private static void Refresh(CardModel card)
    {
        try
        {
            NCard.FindOnTable(card)?.UpdateVisuals(card.Pile?.Type ?? PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh a shaded card: {ex.Message}");
        }
    }
}
