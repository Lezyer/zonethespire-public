using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.Ferrosand;

/// <summary>
/// Magnetic: a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when the card
/// is duplicated). When the card is played, a random other Magnetic card from its owner's draw pile moves to their hand. The
/// pick uses the run seed, the player, their turn number and their pull count this combat (the draw pile order is synced), so
/// every peer moves the same card without consuming a game RNG stream. Each player gets at most 3 pulled cards per turn (6
/// while holding Large Fieldstone); later Magnetic plays that turn pull nothing.
/// </summary>
public sealed class MagneticModifier : CardModifier, ILocalizationProvider
{
    private sealed class PullCounter
    {
        /// <summary>Pull attempts this combat (part of the pick seed, so it never resets).</summary>
        public int Count;

        /// <summary>The turn <see cref="PulledThisTurn"/> counts.</summary>
        public int Turn;

        /// <summary>Cards moved into the hand by Magnetic this turn.</summary>
        public int PulledThisTurn;
    }

    private static readonly ConditionalWeakTable<PlayerCombatState, PullCounter> Pulls = new();

    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool CanMagnetize(CardModel card) =>
        FerrosandRules.CanMagnetize(card.Keywords.Contains(CardKeyword.Unplayable), card.TryGetModifier<MagneticModifier>(out _));

    /// <summary>Makes the card Magnetic if it can be. Safe to call again for the same card. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanMagnetize(card))
        {
            return false;
        }

        AddModifier<MagneticModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = FerrosandRules.MagneticCardText;
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            Player? player = cardPlay.Player;
            PlayerCombatState? combat = player?.PlayerCombatState;
            if (player == null || combat == null)
            {
                return;
            }

            List<CardModel> candidates = combat.DrawPile.Cards
                .Where(card => card != Owner && card.TryGetModifier<MagneticModifier>(out _))
                .ToList();
            if (candidates.Count == 0 || combat.Hand.Cards.Count >= CardPile.MaxCardsInHand)
            {
                return;
            }

            PullCounter counter = Pulls.GetOrCreateValue(combat);
            if (counter.Turn != combat.TurnNumber)
            {
                counter.Turn = combat.TurnNumber;
                counter.PulledThisTurn = 0;
            }

            LargeFieldstone? fieldstone = player.GetRelic<LargeFieldstone>();
            bool hasLargeFieldstone = fieldstone is { IsMelted: false, Status: not RelicStatus.Disabled };
            if (!FerrosandRules.CanPull(counter.PulledThisTurn, hasLargeFieldstone))
            {
                return;
            }

            int index = FerrosandRules.PickPullIndex(player.RunState.Rng.Seed, player.NetId, combat.TurnNumber, counter.Count++, candidates.Count);
            if (index < 0)
            {
                return;
            }

            CardModel pulled = candidates[index];
            await CardPileCmd.Add(pulled, PileType.Hand);
            if (counter.PulledThisTurn >= FerrosandRules.MaxPullsPerTurn)
            {
                fieldstone?.Flash();
            }

            counter.PulledThisTurn++;
        }
        catch (Exception ex)
        {
            Log.Warn($"Magnetic failed to pull a card: {ex}");
        }
    }
}
