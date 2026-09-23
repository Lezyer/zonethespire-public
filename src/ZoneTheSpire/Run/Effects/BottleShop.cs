using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>The Fermentory shop Bottle service. Slot text and visuals are shared with other services in <see cref="ShopServices"/>.</summary>
internal static class BottleShop
{
    public static bool IsBottleActive(Player player) =>
        ZoneEffectQuery.IsActive(player.RunState, FermentoryBiome.ShopEffectId);

    /// <summary>
    /// Replaces OneOffSynchronizer.DoMerchantCardRemoval in Fermentory shops (same peers, same message): +1 potion slot for the
    /// slot's price. Unlike removal it never counts toward the card removal price.
    /// </summary>
    public static async Task<bool> Buy(Player player, int goldCost)
    {
        await PlayerCmd.GainMaxPotionCount(1, player);
        await PlayerCmd.LoseGold(goldCost, player, GoldLossType.Spent);
        return true;
    }
}
