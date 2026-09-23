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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hallowed;

/// <summary>
/// Hallowing (card modifier, attacks only): when played, every enemy the attack damaged gets that much Hallowed, blocked
/// damage included (like Shadow Brutality). A permanent BaseLib CardModifier (saved with the deck, synced, copied when the
/// card is duplicated), a flag with no amount.
/// </summary>
public sealed class HallowingModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool Has(CardModel? card) => card != null && card.TryGetModifier<HallowingModifier>(out _);

    internal static bool CanHallow(CardModel card) => card.Type == CardType.Attack && !card.Keywords.Contains(CardKeyword.Unplayable);

    /// <summary>Adds Hallowing to an eligible attack that lacks it. Safe to call again. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanHallow(card) || Has(card))
        {
            return false;
        }

        AddModifier<HallowingModifier>(card);
        return true;
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = HallowedText.CardLine(HallowedText.Hallowing);
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
        if (!NestedTooltips.Enabled)
        {
            tips.Add(HoverTipFactory.FromPower<HallowedPower>());
        }
    }

    public override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Hallowing.Apply(choiceContext, cardPlay);
}

/// <summary>
/// The damage a Hallowing card dealt to each enemy during the play in progress (filled from the synced damage hook through
/// ZoneTheSpireModifier), turned into Hallowed once the play has finished. Each play of a series has its own tally.
/// </summary>
internal static class Hallowing
{
    private static readonly ConditionalWeakTable<CardModel, Dictionary<Creature, int>> Damage = new();

    /// <summary>Each play (including replays in a series) starts with nothing remembered.</summary>
    public static void BeforeCardPlayed(CardPlay cardPlay) => Damage.Remove(cardPlay.Card);

    public static void AfterDamageGiven(Creature target, ValueProp props, CardModel? cardSource, int damage)
    {
        if (cardSource == null || damage <= 0 || !props.IsPoweredAttack() || target.Side != CombatSide.Enemy || !HallowingModifier.Has(cardSource))
        {
            return;
        }

        Dictionary<Creature, int> dealt = Damage.GetValue(cardSource, static _ => new Dictionary<Creature, int>());
        dealt[target] = dealt.TryGetValue(target, out int existing) ? existing + damage : damage;
    }

    public static async Task Apply(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Player?.Creature is not { } player || !CombatManager.Instance.IsInProgress
                || !Damage.TryGetValue(cardPlay.Card, out Dictionary<Creature, int>? dealt))
            {
                return;
            }

            foreach ((Creature target, int amount) in dealt.Where(entry => entry.Key.IsAlive && entry.Value > 0).ToList())
            {
                await PowerCmd.Apply<HallowedPower>(choiceContext, target, amount, player, cardPlay.Card);
            }

            Damage.Remove(cardPlay.Card);
        }
        catch (Exception ex)
        {
            Log.Warn($"Hallowing failed to apply Hallowed: {ex}");
        }
    }
}
