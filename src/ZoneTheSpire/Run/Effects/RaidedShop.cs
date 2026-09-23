using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Phantasmal Tombs shops: right after each player's shop inventory is created (MerchantRoom.Enter, on every peer), each card,
/// relic and potion slot is emptied with a 50% chance decided by the run seed, shop location, player and slot, exactly like a
/// sold-out slot, and every card the ghosts leave behind becomes Phantasm-Haunted. Card removal is never taken. Prices are
/// halved by <see cref="RaidedShopHandler"/>.
/// </summary>
internal static class RaidedShop
{
    private static readonly MethodInfo? ClearMethod = AccessTools.Method(typeof(MerchantEntry), "ClearAfterPurchase");

    public static void Raid(MerchantInventory inventory)
    {
        Player player = inventory.Player;
        IRunState runState = player.RunState;
        if (ClearMethod == null || !ZoneEffectQuery.IsActive(runState, PhantasmalTombsBiome.RaidedShopEffectId))
        {
            return;
        }

        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        RaidAll("card", inventory.CharacterCardEntries, 0);
        RaidAll("card", inventory.ColorlessCardEntries, inventory.CharacterCardEntries.Count);
        RaidAll("relic", inventory.RelicEntries, 0);
        RaidAll("potion", inventory.PotionEntries, 0);
        HauntLeftovers(inventory.CharacterCardEntries);
        HauntLeftovers(inventory.ColorlessCardEntries);

        void RaidAll<T>(string category, IReadOnlyList<T> entries, int firstSlot)
            where T : MerchantEntry
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MerchantEntry entry = entries[i];
                int slot = firstSlot + i;
                if (entry.IsStocked && PhantasmalRules.IsRaided(runState.Rng.Seed, location, player.NetId, category, slot))
                {
                    ClearMethod!.Invoke(entry, null);
                }
            }
        }

        void HauntLeftovers(IEnumerable<MerchantCardEntry> entries)
        {
            foreach (MerchantCardEntry entry in entries)
            {
                if (entry.IsStocked && entry.CreationResult?.Card is { } card)
                {
                    PhantasmHauntedModifier.TryAdd(card);
                }
            }
        }
    }
}

/// <summary>Phantasmal Tombs shops: cards, relics and potions cost 50% less (card removal is unchanged).</summary>
internal sealed class RaidedShopHandler : ZoneEffectHandler
{
    public override string EffectId => PhantasmalTombsBiome.RaidedShopEffectId;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) =>
        entry is MerchantCardEntry or MerchantRelicEntry or MerchantPotionEntry ? PhantasmalRules.DiscountedPrice(cost) : cost;
}
