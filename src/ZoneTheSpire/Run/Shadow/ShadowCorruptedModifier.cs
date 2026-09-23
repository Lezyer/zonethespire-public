using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// Shadow Corrupted (card): a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied
/// when the card is duplicated). Every value on the card is doubled (see <see cref="ShadowCorruption"/>): damage and Block
/// after enchantments (before Strength and Dexterity), every other number on the card and its enchantment (powers, energy,
/// draw, Stars, Forge, Summon, gold, healing), and its Wriggling and Gilded amounts. When played (first play of a series
/// only), its owner gains Doom: 5 per energy paid, 3 if the cost was 0.
/// </summary>
public sealed class ShadowCorruptedModifier : CardModifier, ILocalizationProvider
{
    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool IsCorrupted(CardModel? card) => card != null && card.TryGetModifier<ShadowCorruptedModifier>(out _);

    /// <summary>Corrupts the card if it has a value to double and isn't corrupted yet. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!ShadowCorruption.CanCorrupt(card))
        {
            return false;
        }

        AddModifier<ShadowCorruptedModifier>(card);
        return true;
    }

    public override void OnInitialApplication() => ShadowCorruption.EnsureDoubled(Owner);

    public override void AfterClonedOnCard(CardModel card) => ShadowCorruption.EnsureDoubled(card);

    public override decimal ModifyBaseDamageMultiplicative(decimal originalDamage, ValueProp props) =>
        props.IsPoweredAttack() ? ShadowCorruptionRules.ValueMultiplier : 1m;

    public override void ModifyDescription(Creature? target, ref string description)
    {
        if (Owner == null)
        {
            return;
        }

        string line = ShadowCorruptionRules.CardText(Owner.EnergyCost.CostsX, Owner.EnergyCost.GetResolved());
        description = string.IsNullOrEmpty(description) ? line : description + "\n" + line;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
        if (!NestedTooltips.Enabled)
        {
            tips.Add(HoverTipFactory.FromPower<DoomPower>());
        }
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (!cardPlay.IsFirstInSeries || cardPlay.Player is not Player player || player.Creature is not { IsDead: false } creature)
            {
                return;
            }

            int doom = ShadowCorruptionRules.DoomFor(cardPlay.Resources.EnergySpent);
            await PowerCmd.Apply<DoomPower>(choiceContext, creature, doom, creature, cardPlay.Card);
        }
        catch (Exception ex)
        {
            Log.Warn($"Shadow Corrupted failed to apply Doom: {ex}");
        }
    }
}
