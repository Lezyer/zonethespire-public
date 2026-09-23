using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BaseLib.Extensions;
using BaseLib.Patches.Localization;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// Karma (see KarmaRules): a per-card value that only exists in combat. It lives on the combat copy of a card, so it resets
/// after every fight; a card starts with Karma equal to its Chakra (read the first time its Karma is needed, which gives the
/// same value on every peer). Every change runs in synced combat hooks (card play, turn end), so every peer agrees.
/// <list type="bullet">
/// <item>Normal cards: the cost hook (ZoneTheSpireModifier.TryModifyEnergyCostInCombat) lowers or raises the cost, so the cost
/// shown in the corner is the cost with Karma. When the card is paid for, the discount actually used is spent
/// (KarmaSpendPatch). A card that costs 0 has no discount to spend, so it passes 1 Karma to a random card in hand instead.</item>
/// <item>X-cost cards: when played, the play's X bonus is captured (+1 for positive Karma, spending 1; the whole negative Karma
/// otherwise) and added to X by ModifyXValue for that play; outside a play, previews use the card's current Karma.</item>
/// <item>Samsara: after that, a played card loses 1 more Karma; at the end of the turn, negative-Karma cards left unplayed in
/// hand get 1 back.</item>
/// </list>
/// Karma works wherever the card is, zone or not; only Samsara is specific to Deva's Domain fights.
/// </summary>
internal static class Karma
{
    private sealed class State
    {
        public int Value;

        /// <summary>The X bonus captured for the play in progress (X-cost cards), or null outside a play.</summary>
        public int? PlayXBonus;

        public PlayerCombatState? PlayedIn;

        public int PlayedTurn;
    }

    private static readonly ConditionalWeakTable<CardModel, State> States = new();

    /// <summary>Set while measuring a card's cost without its Karma.</summary>
    [ThreadStatic]
    private static bool _suppressed;

    /// <summary>The card's Karma: 0 outside combat.</summary>
    public static int Get(CardModel? card) => card != null && StateOf(card) is { } state ? state.Value : 0;

    private static State? StateOf(CardModel card)
    {
        if (!card.IsMutable || card.CombatState == null)
        {
            return null;
        }

        return States.GetValue(card, static card => new State { Value = ChakraModifier.AmountOf(card) });
    }

    private static void Set(CardModel card, State state, int value)
    {
        if (state.Value == value)
        {
            return;
        }

        state.Value = value;
        Refresh(card);
    }

    /// <summary>Gives a combat card more Karma (Scroll of Chants). Does nothing outside combat.</summary>
    public static void Add(CardModel card, int amount)
    {
        if (amount != 0 && StateOf(card) is { } state)
        {
            Set(card, state, state.Value + amount);
        }
    }

    /// <summary>Cost hook: a normal card's cost with its Karma.</summary>
    public static bool TryModifyCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_suppressed || card.EnergyCost.CostsX || originalCost < 0m)
        {
            return false;
        }

        int karma = Get(card);
        if (karma == 0)
        {
            return false;
        }

        modifiedCost = KarmaRules.CostWithKarma((int)originalCost, karma);
        return modifiedCost != originalCost;
    }

    /// <summary>
    /// Called just after a normal card was paid for (manual plays): spends the discount its Karma actually gave, measured as its
    /// cost without Karma minus the cost paid.
    /// </summary>
    public static void AfterPaid(CardModel card)
    {
        try
        {
            if (card.EnergyCost.CostsX || StateOf(card) is not { Value: > 0 } state)
            {
                return;
            }

            int withKarma = card.EnergyCost.GetWithModifiers(CostModifiers.All);
            int spent = KarmaRules.KarmaSpent(state.Value, CostWithoutKarma(card), withKarma);
            if (spent > 0)
            {
                Set(card, state, state.Value - spent);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to spend a card's Karma: {ex}");
        }
    }

    /// <summary>
    /// Before the first play of a card play series: X-cost cards capture this play's X bonus (spending 1 positive Karma), then
    /// Samsara takes 1 Karma from the card and marks it as played this turn.
    /// </summary>
    public static void BeforeCardPlayed(CardPlay cardPlay)
    {
        try
        {
            CardModel card = cardPlay.Card;
            if (cardPlay.PlayIndex != 0 || StateOf(card) is not { } state)
            {
                return;
            }

            if (card.EnergyCost.CostsX)
            {
                state.PlayXBonus = KarmaRules.XBonusFor(state.Value);
                int spent = KarmaRules.XKarmaSpent(state.Value);
                if (spent > 0)
                {
                    Set(card, state, state.Value - spent);
                }
            }

            GiveAwayFromZeroCost(cardPlay, state);

            if (cardPlay.Player is { } player && SamsaraPower.IsOn(player))
            {
                Set(card, state, state.Value - KarmaRules.SamsaraKarmaLossPerPlay);
                state.PlayedIn = player.PlayerCombatState;
                state.PlayedTurn = player.PlayerCombatState?.TurnNumber ?? 0;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply Karma to a card play: {ex}");
        }
    }

    /// <summary>Karma passed on from 0-cost cards this combat, per player: it keeps the random picks apart.</summary>
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> Gifts = new();

    /// <summary>
    /// A 0-cost card has no discount to spend, so playing it passes 1 of its Karma to a random card in hand (the same card on
    /// every peer). With an empty hand it keeps that Karma. The card is already out of the hand by now, so it never picks itself.
    /// </summary>
    private static void GiveAwayFromZeroCost(CardPlay cardPlay, State state)
    {
        if (cardPlay.Player is not { } owner || owner.PlayerCombatState is not { } combat)
        {
            return;
        }

        List<CardModel> hand = combat.Hand.Cards.ToList();
        if (!KarmaRules.GivesKarmaAway(state.Value, CostWithoutKarma(cardPlay.Card), cardPlay.Card.EnergyCost.CostsX, hand.Count))
        {
            return;
        }

        StrongBox<int> gifts = Gifts.GetValue(combat, static _ => new StrongBox<int>(0));
        int index = KarmaRules.PickKarmaGiftIndex(owner.RunState.Rng.Seed, owner.NetId, combat.TurnNumber, gifts.Value, hand.Count);
        if (index < 0 || StateOf(hand[index]) is not { } target)
        {
            return;
        }

        gifts.Value++;
        Set(cardPlay.Card, state, state.Value - 1);
        Set(hand[index], target, target.Value + 1);
    }

    /// <summary>The card's cost without its Karma (its Karma is left out of the cost hook while this is measured).</summary>
    private static int CostWithoutKarma(CardModel card)
    {
        _suppressed = true;
        try
        {
            return card.EnergyCost.GetWithModifiers(CostModifiers.All);
        }
        finally
        {
            _suppressed = false;
        }
    }

    /// <summary>After the last play of a series, X previews go back to the card's current Karma.</summary>
    public static void AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.PlayIndex >= cardPlay.PlayCount - 1 && States.TryGetValue(cardPlay.Card, out State? state))
        {
            state.PlayXBonus = null;
        }
    }

    /// <summary>X hook: X with the play's captured Karma bonus (or, in previews, the card's current Karma).</summary>
    public static int ModifyX(CardModel card, int value)
    {
        if (!card.EnergyCost.CostsX || StateOf(card) is not { } state)
        {
            return value;
        }

        int bonus = state.PlayXBonus ?? KarmaRules.XBonusFor(state.Value);
        return KarmaRules.XWithKarma(value, bonus);
    }

    /// <summary>
    /// Samsara, at the end of the player's turn (before the hand is discarded): each card in hand with negative Karma that wasn't
    /// played this turn gets 1 back.
    /// </summary>
    public static void RecoverUnplayed(Player player)
    {
        try
        {
            if (player.PlayerCombatState is not { } combat)
            {
                return;
            }

            foreach (CardModel card in combat.Hand.Cards.ToList())
            {
                if (StateOf(card) is not { Value: < 0 } state
                    || (ReferenceEquals(state.PlayedIn, combat) && state.PlayedTurn == combat.TurnNumber))
                {
                    continue;
                }

                Set(card, state, KarmaRules.Recover(state.Value));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to recover Karma at the end of the turn: {ex}");
        }
    }

    /// <summary>Adds the Karma line to every card's text (BaseLib's description hook, after the card modifiers' lines).</summary>
    public static void RegisterCardText()
    {
        DescriptionOverrides.CustomizeDescription += (CardModel card, Creature? _, ref string description) =>
        {
            try
            {
                AddCardText(card, ref description);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to add a card's Karma line: {ex.Message}");
            }
        };
    }

    /// <summary>The "Karma N" line near the top of a card with Karma (in combat, when not 0): just below its Chakra line, if any.</summary>
    private static void AddCardText(CardModel card, ref string description)
    {
        int karma = Get(card);
        if (karma == 0)
        {
            return;
        }

        string line = KarmaRules.KarmaText(karma);
        if (string.IsNullOrEmpty(description))
        {
            description = line;
            return;
        }

        int chakra = ChakraModifier.AmountOf(card);
        string chakraLine = KarmaRules.ChakraText(chakra);
        int at = chakra > 0 ? description.IndexOf(chakraLine, StringComparison.Ordinal) : -1;
        description = at < 0
            ? line + "\n" + description
            : description.Insert(at + chakraLine.Length, "\n" + line);
    }

    public static IHoverTip Tip => new HoverTip(
        ZoneTheSpireModifier.ModTextLoc("devas.karma"),
        ZoneTheSpireModifier.ModTextLoc("devas.karma_description"));

    /// <summary>The Karma tip for one card: the 0-cost and X-cost rules are added only on the cards they apply to.</summary>
    public static IHoverTip TipFor(CardModel card)
    {
        string note = card.EnergyCost.CostsX ? DevasText.KarmaXCostNote
            : CostWithoutKarma(card) == 0 ? DevasText.KarmaZeroCostNote
            : string.Empty;
        return note.Length == 0 ? Tip : new HoverTip(ZoneTheSpireModifier.ModTextLoc("devas.karma"), DevasText.KarmaDescription + " " + note);
    }

    /// <summary>The Karma tip on a card with Karma or Chakra.</summary>
    public static bool NeedsTip(CardModel card) => Get(card) != 0 || ChakraModifier.AmountOf(card) > 0;

    /// <summary>Redraws the card's cost and text wherever it is on screen (local only).</summary>
    private static void Refresh(CardModel card)
    {
        try
        {
            card.InvokeEnergyCostChanged();
            NCard.FindOnTable(card)?.UpdateVisuals(card.Pile?.Type ?? PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh a card's Karma: {ex.Message}");
        }
    }
}
