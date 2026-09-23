using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TextEffects;
using ZoneTheSpire.Core.ForgottenEmpire;

namespace ZoneTheSpire.Run.ForgottenEmpire;

/// <summary>
/// Marbled (card): a permanent card modifier (BaseLib CardModifier: saved with the deck, synced in multiplayer, copied when the
/// card is duplicated). The card gives half its Block (MarbledBlockGainPatch) plus its full Block as Marbled. Its text shows
/// the halved Block and an extra "Gain X Marbled." line: MarbledCardPreviewPatch halves only the displayed Block values after
/// the game computes them (gameplay reads the unchanged base value).
/// </summary>
public sealed class MarbledModifier : CardModifier, ILocalizationProvider
{
    private sealed class DisplayState
    {
        public decimal Base = decimal.MinValue;
        public decimal FullEnchanted;
        public decimal HalvedEnchanted;
    }

    /// <summary>Per Block value: what the halving last wrote, so a stale halved value is never halved twice.</summary>
    private static readonly ConditionalWeakTable<DynamicVar, DisplayState> Displays = new();

    /// <summary>The full (unhalved) Block the card last previewed, shown as its Marbled gain, and how it compares to the base.</summary>
    private int? _previewMarbled;
    private int _previewComparison;

    public string? LocTable => "card_modifiers";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    internal static bool CanMarble(CardModel card) =>
        ForgottenEmpireRules.CanMarble(MarbleCards.GainsBlock(card), MarbleCards.IsMarbled(card));

    /// <summary>Sculpt can pick cards with a Block value that aren't Marbled yet.</summary>
    internal static bool CanSculpt(CardModel card) => MarbleCards.HasBlockVar(card) && !MarbleCards.IsMarbled(card);

    /// <summary>Makes the card Marbled if it can be. Safe to call again for the same card. Returns whether it was added.</summary>
    internal static bool TryAdd(CardModel card)
    {
        if (!CanMarble(card))
        {
            return false;
        }

        AddModifier<MarbledModifier>(card);
        return true;
    }

    /// <summary>Sculpt: makes the card Marbled. Returns whether it was sculpted.</summary>
    internal static bool Sculpt(CardModel card)
    {
        if (!CanSculpt(card))
        {
            return false;
        }

        AddModifier<MarbledModifier>(card);
        return true;
    }

    /// <summary>
    /// Called after the game refreshed the card's displayed values: each Block value shows half (what the card really gives),
    /// and the full value is kept for the "Gain X Marbled." line. Display values only; gameplay uses the base value.
    /// </summary>
    internal static void AfterPreviewUpdated(CardModel card)
    {
        if (!card.TryGetModifier(out MarbledModifier? modifier) || modifier == null)
        {
            return;
        }

        BlockVar? primary = PrimaryBlock(card);
        foreach (BlockVar block in card.DynamicVars.Values.OfType<BlockVar>())
        {
            DisplayState state = Displays.GetOrCreateValue(block);
            // Without an enchantment the unmodified value is the base. With one, the game only rewrites it outside enchantment
            // previews, so a value equal to what we last wrote is ours and stands for the full value we kept.
            // (A Shadow Corrupted card shows its doubled Block as its unmodified value.)
            decimal enchanted = card.Enchantment == null ? Shadow.ShadowCorruption.ModifyCardBlock(block.BaseValue, block.Props, card)
                : state.Base == block.BaseValue && block.EnchantedValue == state.HalvedEnchanted ? state.FullEnchanted
                : block.EnchantedValue;
            decimal full = block.PreviewValue;
            block.PreviewValue = ForgottenEmpireRules.MarbledCardBlock((int)full);
            block.EnchantedValue = ForgottenEmpireRules.MarbledCardBlock((int)enchanted);
            state.Base = block.BaseValue;
            state.FullEnchanted = enchanted;
            state.HalvedEnchanted = block.EnchantedValue;
            if (block == primary)
            {
                modifier._previewMarbled = (int)full;
                modifier._previewComparison = ((int)full).CompareTo((int)enchanted);
            }
        }
    }

    private static BlockVar? PrimaryBlock(CardModel card)
    {
        List<BlockVar> blocks = card.DynamicVars.Values.OfType<BlockVar>().ToList();
        return blocks.FirstOrDefault(block => block.Name == BlockVar.defaultName) ?? blocks.FirstOrDefault();
    }

    public override void ModifyDescription(Creature? target, ref string description)
    {
        BlockVar? primary = Owner == null ? null : PrimaryBlock(Owner);
        string line;
        if (primary == null)
        {
            line = ForgottenEmpireRules.MarbledCardText;
        }
        else
        {
            int amount = _previewMarbled ?? (int)primary.BaseValue;
            int comparison = primary.WasJustUpgraded ? 1 : _previewComparison;
            line = ForgottenEmpireRules.MarbledGainText(StsTextUtilities.HighlightChangeText(amount.ToString(CultureInfo.InvariantCulture), comparison));
        }

        description = string.IsNullOrEmpty(description) ? line : description + "\n" + line;
    }

    public override void AddTips(List<IHoverTip> tips)
    {
        tips.Add(new HoverTip(GetLoc("title"), GetLoc("description")));
    }
}
