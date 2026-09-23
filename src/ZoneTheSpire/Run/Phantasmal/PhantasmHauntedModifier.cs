using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Phantasmal;

namespace ZoneTheSpire.Run.Phantasmal;

/// <summary>
/// Phantasm-Haunted: a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when
/// the card is duplicated). The card's own cost is 1 lower (so the deck, rewards, shop and hand all show it), the card counts
/// as Ethereal (PhantasmHauntedKeywordPatch adds the keyword, so the game exhausts it from the hand at the end of the turn),
/// and when played it exhausts with a 30% chance (ZoneTheSpireModifier dispatches the roll).
/// <para>
/// The game never saves a card's cost (it is rebuilt from the printed cost and upgrades, and upgrades change it relative to
/// the current cost), so the reduction is applied once when the card is haunted, re-applied when the modifier is loaded from
/// a save or a multiplayer packet and after a downgrade (which resets the cost), and not re-applied on duplicated cards
/// (their cost is copied already lowered).
/// </para>
/// </summary>
public sealed class PhantasmHauntedModifier : CardModifier, ILocalizationProvider
{
    private const string ReductionKey = "Reduction";

    /// <summary>How much this haunting lowered the card's cost: 1, or 0 when it already cost 0.</summary>
    private int _reduction;

    /// <summary>True once the reduction is on this card's cost (copied along when the card is duplicated).</summary>
    private bool _applied;

    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    /// <summary>The card's own cost (upgrades and haunting included, combat modifiers excluded).</summary>
    private static int BaseCost(CardModel card) => card.EnergyCost.GetWithModifiers(CostModifiers.None);

    /// <summary>Whether the card carries the haunting.</summary>
    internal static bool IsHaunted(CardModel? card) => card != null && card.TryGetModifier<PhantasmHauntedModifier>(out _);

    /// <summary>The card's keywords with Ethereal added (never the card's own set, which must not be changed here).</summary>
    internal static IReadOnlySet<CardKeyword> WithEthereal(IReadOnlySet<CardKeyword> keywords)
    {
        if (keywords.Contains(CardKeyword.Ethereal))
        {
            return keywords;
        }

        var withEthereal = new HashSet<CardKeyword>(keywords) { CardKeyword.Ethereal };
        return withEthereal;
    }

    internal static bool CanHaunt(CardModel card) =>
        PhantasmalRules.CanHaunt(
            card.EnergyCost.CostsX,
            BaseCost(card),
            card.Keywords.Contains(CardKeyword.Unplayable),
            IsHaunted(card));

    /// <summary>Haunts the card if it can be haunted. Safe to call again for the same card. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanHaunt(card))
        {
            return false;
        }

        AddModifier<PhantasmHauntedModifier>(card);
        return true;
    }

    public override void OnInitialApplication()
    {
        // Duplicated cards arrive with the reduction already applied (and their cost already lowered).
        if (_applied || Owner == null)
        {
            return;
        }

        _reduction = !Owner.EnergyCost.CostsX && BaseCost(Owner) > 0 ? PhantasmalRules.HauntedCostReduction : 0;
        LowerCost();
    }

    public override void StoreSaveData(ModifierSave save)
    {
        save.IntProperties[ReductionKey] = _reduction;
    }

    public override void LoadSaveData(ModifierSave save)
    {
        if (Owner == null)
        {
            return;
        }

        _reduction = save.IntProperties.TryGetValue(ReductionKey, out int reduction)
            ? reduction
            : BaseCost(Owner) > 0 ? PhantasmalRules.HauntedCostReduction : 0;
        LowerCost();
    }

    public override void OnDowngrade()
    {
        base.OnDowngrade();
        // A downgrade resets the cost to the printed cost; lower it again.
        LowerCost();
    }

    private void LowerCost()
    {
        _applied = true;
        if (Owner == null || _reduction <= 0 || Owner.EnergyCost.CostsX)
        {
            return;
        }

        Owner.EnergyCost.SetCustomBaseCost(PhantasmalRules.HauntedCost(BaseCost(Owner)));
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        string line = Owner != null && Owner.EnergyCost.CostsX ? PhantasmalRules.HauntedXCardText : PhantasmalRules.HauntedCardText;
        description = string.IsNullOrEmpty(description) ? line : line + "\n" + description;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc(Owner != null && Owner.EnergyCost.CostsX ? "descriptionX" : "description")));
    }

    /// <summary>
    /// The 30% exhaust roll, dispatched from <see cref="ZoneTheSpireModifier.ModifyCardPlayResultLocation"/> (the combat hook
    /// listener): a haunted card that would return to its owner's discard pile instead exhausts when the roll hits. Powers (and
    /// duplicated copies) leave play either way and never roll. The roll consumes the run's Niche stream inside the synced card
    /// play, so every peer rolls the same result and it survives save & quit.
    /// </summary>
    internal static CardLocation ModifyPlayResultLocation(CardModel card, CardLocation cardLocation)
    {
        if (cardLocation.pileType != PileType.Discard || card.IsDupe || card.Type == CardType.Power || !IsHaunted(card) || card.Owner is not { } player)
        {
            return cardLocation;
        }

        if (player.RunState.Rng.Niche.NextInt(100) < PhantasmalRules.HauntedExhaustChancePercent)
        {
            return new CardLocation(player, PileType.Exhaust, CardPilePosition.Bottom);
        }

        return cardLocation;
    }
}
