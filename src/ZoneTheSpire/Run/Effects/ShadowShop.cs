using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Shadow Corruption shops: right after each player's shop inventory is created (on every peer), 4 card slots picked from the
/// run seed, shop location and player have their card shaded, 3 eligible cards (picked independently, so a card can be both) are
/// Shadow Corrupted, and every relic and potion slot is hidden. Shaded cards and hidden slots cost 60% less
/// (<see cref="ShadowShopHandler"/>). A bought card is un-shaded (it joins the deck face up) and stays corrupted; a restocked
/// card is neither. The hidden look is local presentation (ShadowShopPatches).
/// </summary>
internal static class ShadowShop
{
    private static readonly ConditionalWeakTable<MerchantEntry, object> Hidden = new();

    public static void Shroud(MerchantInventory inventory)
    {
        Player player = inventory.Player;
        IRunState runState = player.RunState;
        if (!ZoneEffectQuery.IsActive(runState, ShadowCorruptionBiome.ShadowShopEffectId))
        {
            return;
        }

        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        List<MerchantCardEntry> cards = inventory.CardEntries.ToList();
        foreach (int slot in ShadowRules.PickShopShadedSlots(runState.Rng.Seed, location, player.NetId, cards.Count))
        {
            if (cards[slot].CreationResult?.Card is { } card)
            {
                ShadedCards.Shade(card);
            }
        }

        foreach (MerchantEntry entry in inventory.RelicEntries.Cast<MerchantEntry>().Concat(inventory.PotionEntries))
        {
            Hidden.AddOrUpdate(entry, entry);
        }

        ShadowCorruption.CorruptShopCards(
            player,
            cards.Select(entry => entry.CreationResult?.Card).OfType<CardModel>().ToList());
    }

    /// <summary>Whether this relic or potion slot is hidden (its item is shown as a shrouded icon until bought).</summary>
    public static bool IsHidden(MerchantEntry? entry) => entry != null && Hidden.TryGetValue(entry, out _);

    /// <summary>Whether this slot sells at the Shadow Corruption discount: a shaded card or a hidden relic or potion.</summary>
    public static bool IsDiscounted(MerchantEntry entry) =>
        entry is MerchantCardEntry cardEntry ? ShadedCards.IsShaded(cardEntry.CreationResult?.Card) : IsHidden(entry);
}

/// <summary>Shadow Corruption shop prices: shaded cards and hidden relics and potions cost 60% less.</summary>
internal sealed class ShadowShopHandler : ZoneEffectHandler
{
    public override string EffectId => ShadowCorruptionBiome.ShadowShopEffectId;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) =>
        ShadowShop.IsDiscounted(entry) ? ShadowRules.DiscountedPrice(cost) : cost;
}
