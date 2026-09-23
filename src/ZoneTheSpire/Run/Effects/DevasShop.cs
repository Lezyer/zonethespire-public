using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Devas;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Deva's Domain shops: right after each player's shop inventory is created (on every peer), its cards are taken out and it gets
/// 3 more relics and 5 more potions (6 and 8 in all), rolled like the vanilla ones from the player's shop RNG, so every peer
/// gets the same stock. Card removal stays. The screen lays the extra slots out where the cards were (DevasShopLayoutPatch).
/// </summary>
internal static class DevasShop
{
    private static readonly FieldInfo? CharacterCardsField = AccessTools.Field(typeof(MerchantInventory), "_characterCardEntries");
    private static readonly FieldInfo? ColorlessCardsField = AccessTools.Field(typeof(MerchantInventory), "_colorlessCardEntries");
    private static readonly FieldInfo? PotionsField = AccessTools.Field(typeof(MerchantInventory), "_potionEntries");
    private static readonly MethodInfo? UpdateEntriesMethod = AccessTools.Method(typeof(MerchantInventory), "UpdateEntries");

    private static readonly ConditionalWeakTable<MerchantInventory, object> Restocked = new();

    /// <summary>Whether this inventory is a Deva's Domain shop (cards taken out, extra relics and potions).</summary>
    public static bool IsDevasShop(MerchantInventory? inventory) => inventory != null && Restocked.TryGetValue(inventory, out _);

    public static void Restock(MerchantInventory inventory)
    {
        Player player = inventory.Player;
        if (!ZoneEffectQuery.IsActive(player.RunState, DevasDomainBiome.DevasShopEffectId))
        {
            return;
        }

        if (CharacterCardsField?.GetValue(inventory) is not List<MerchantCardEntry> characterCards
            || ColorlessCardsField?.GetValue(inventory) is not List<MerchantCardEntry> colorlessCards
            || PotionsField?.GetValue(inventory) is not List<MerchantPotionEntry> potions
            || UpdateEntriesMethod == null)
        {
            Log.Warn("Deva's Domain shop: the shop inventory layout changed; the shop is left as it is.");
            return;
        }

        characterCards.Clear();
        colorlessCards.Clear();

        var added = new List<MerchantEntry>();
        for (int i = inventory.RelicEntries.Count; i < KarmaRules.ShopRelics; i++)
        {
            var relic = new MerchantRelicEntry(RelicFactory.RollRarity(player), player);
            inventory.AddRelicEntry(relic);
            added.Add(relic);
        }

        int morePotions = KarmaRules.ShopPotions - potions.Count;
        if (morePotions > 0)
        {
            List<PotionModel> stocked = potions
                .Select(entry => entry.Model)
                .OfType<PotionModel>()
                .Select(model => ModelDb.GetByIdOrNull<PotionModel>(model.Id))
                .OfType<PotionModel>()
                .ToList();
            foreach (PotionModel potion in PotionFactory.CreateRandomPotionsOutOfCombat(player, morePotions, player.PlayerRng.Shops, stocked).ToList())
            {
                var entry = new MerchantPotionEntry(potion.ToMutable(), player);
                potions.Add(entry);
                added.Add(entry);
            }
        }

        // Like CreateForNormalMerchant: every entry refreshes the others (prices, affordability) after a purchase.
        var update = (Action<PurchaseStatus, MerchantEntry>)Delegate.CreateDelegate(typeof(Action<PurchaseStatus, MerchantEntry>), inventory, UpdateEntriesMethod);
        foreach (MerchantEntry entry in added)
        {
            entry.PurchaseCompleted += update;
        }

        Restocked.AddOrUpdate(inventory, inventory);
    }
}

/// <summary>Deva's Domain shop: the stock change happens when the inventory is created (DevasShop.Restock); prices are vanilla.</summary>
internal sealed class DevasShopHandler : ZoneEffectHandler
{
    public override string EffectId => DevasDomainBiome.DevasShopEffectId;
}
