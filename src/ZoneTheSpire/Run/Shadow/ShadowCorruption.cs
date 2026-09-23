using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.HallsOfMidas;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// How a Shadow Corrupted card's values are doubled:
/// <list type="bullet">
/// <item>Damage: BaseLib's card modifier damage hook (ShadowCorruptedModifier), which runs after the enchantment and before
/// powers like Strength, in combat and in every preview.</item>
/// <item>Block: the same place in the game's block calculation (ShadowCorruptionPatches), plus the Block previews.</item>
/// <item>Every other number on the card or its enchantment (powers, energy, draw, Stars, Forge, Summon, gold, healing): the
/// stored value is doubled once per value instance. A doubled value is remembered (and so are its clones), so it is never
/// doubled twice, and later upgrades of it are doubled too. Values rebuilt from the canonical card (loading a save,
/// resetting a card) are doubled again the next time the card is previewed, cloned or played.</item>
/// </list>
/// Never doubled: hit counts, play counts, HP loss and Max HP costs, and plain counters.
/// </summary>
internal static class ShadowCorruption
{
    private static readonly ConditionalWeakTable<DynamicVar, object> Doubled = new();

    public static bool IsDoubled(DynamicVar dynamicVar) => Doubled.TryGetValue(dynamicVar, out _);

    /// <summary>A clone of a doubled value already holds the doubled number.</summary>
    public static void CopyDoubled(DynamicVar source, DynamicVar clone)
    {
        if (IsDoubled(source))
        {
            Doubled.AddOrUpdate(clone, clone);
        }
    }

    /// <summary>Numbers doubled by storing them doubled (damage and Block are doubled where they are calculated instead).</summary>
    public static bool IsStoredDoubleType(DynamicVar dynamicVar) =>
        dynamicVar is EnergyVar or CardsVar or StarsVar or ForgeVar or SummonVar or GoldVar or HealVar || IsPowerVar(dynamicVar);

    private static bool IsPowerVar(DynamicVar dynamicVar)
    {
        for (Type? type = dynamicVar.GetType(); type != null && type != typeof(DynamicVar); type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PowerVar<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDamageOrBlock(DynamicVar dynamicVar) =>
        dynamicVar is DamageVar or CalculatedDamageVar or ExtraDamageVar or OstyDamageVar or BlockVar or CalculatedBlockVar;

    /// <summary>Whether the card has anything to double: a damage, Block or other doubled value, or Wriggling or Gilded.</summary>
    public static bool CanCorrupt(CardModel card)
    {
        if (card.Type is CardType.Status or CardType.Curse
            || card.Keywords.Contains(CardKeyword.Unplayable)
            || ShadowCorruptedModifier.IsCorrupted(card))
        {
            return false;
        }

        return Values(card).Any(value => IsDamageOrBlock(value) || IsStoredDoubleType(value))
            || card.TryGetModifier<WrigglingModifier>(out _)
            || card.TryGetModifier<GildedModifier>(out _);
    }

    /// <summary>Doubles every not-yet-doubled stored value of a corrupted card and of its enchantment. Safe to call often.</summary>
    public static void EnsureDoubled(CardModel? card)
    {
        if (card == null || !card.IsMutable || !ShadowCorruptedModifier.IsCorrupted(card))
        {
            return;
        }

        try
        {
            foreach (DynamicVar value in Values(card))
            {
                if (IsStoredDoubleType(value) && !IsDoubled(value))
                {
                    Doubled.AddOrUpdate(value, value);
                    value.BaseValue *= ShadowCorruptionRules.ValueMultiplier;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to double a Shadow Corrupted card's values: {ex.Message}");
        }
    }

    private static IEnumerable<DynamicVar> Values(CardModel card) =>
        card.Enchantment == null ? card.DynamicVars.Values : card.DynamicVars.Values.Concat(card.Enchantment.DynamicVars.Values);

    /// <summary>Block from a corrupted card, after its enchantment and before Dexterity and other powers.</summary>
    public static decimal ModifyCardBlock(decimal block, ValueProp props, CardModel? cardSource) =>
        ShadowCorruptedModifier.IsCorrupted(cardSource) && props.IsPoweredCardOrMonsterMoveBlock()
            ? block * ShadowCorruptionRules.ValueMultiplier
            : block;

    /// <summary>
    /// Inside Shadow Corruption, one eligible card of every card reward is corrupted, picked from the run seed, node, player and
    /// the reward's cards (the same on every peer). Returns whether the reward changed. Rewards that already hold a corrupted card
    /// are left alone (CardReward.Populate can re-invoke the hook), as are rewards that forbid card modifications.
    /// </summary>
    public static bool TryCorruptReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions)
    {
        try
        {
            IRunState runState = player.RunState;
            if (ZoneContext.Current(runState)?.Biome is not ShadowCorruptionBiome
                || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications)
                || options.Any(option => ShadowCorruptedModifier.IsCorrupted(option.Card)))
            {
                return false;
            }

            List<CardModel> eligible = options.Select(option => option.Card).Where(CanCorrupt).ToList();
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            string optionsKey = string.Join(",", options.Select(option => option.Card.Id.Entry));
            int index = ShadowCorruptionRules.PickRewardIndex(runState.Rng.Seed, location, player.NetId, optionsKey, eligible.Count);
            if (index < 0 || !ShadowCorruptedModifier.TryAdd(eligible[index]))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to corrupt a Shadow Corruption card reward: {ex}");
            return false;
        }
    }

    /// <summary>Corrupts 3 eligible cards of a Shadow Corruption shop (picked independently from the shaded ones).</summary>
    public static void CorruptShopCards(Player player, IReadOnlyList<CardModel> cards)
    {
        IRunState runState = player.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        List<CardModel> eligible = cards.Where(CanCorrupt).ToList();
        foreach (int slot in ShadowCorruptionRules.PickShopSlots(runState.Rng.Seed, location, player.NetId, eligible.Count))
        {
            ShadowCorruptedModifier.TryAdd(eligible[slot]);
        }
    }

    /// <summary>Multiplier for a corrupted card's modifier amounts (Wriggling, Gilded gold) and enchantment amounts (Swift, Sown).</summary>
    public static int AmountMultiplier(CardModel? card) => ShadowCorruptedModifier.IsCorrupted(card) ? ShadowCorruptionRules.ValueMultiplier : 1;
}
