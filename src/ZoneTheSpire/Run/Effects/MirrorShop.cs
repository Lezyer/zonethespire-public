using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Mirrorlands shop Duplicate service. Slot text and visuals are shared with other services in <see cref="ShopServices"/>.</summary>
internal static class MirrorShop
{
    public static bool IsDuplicateActive(Player player) =>
        ZoneEffectQuery.IsActive(player.RunState, MirrorlandsBiome.DuplicateServiceEffectId);

    /// <summary>
    /// Replaces OneOffSynchronizer.DoMerchantCardRemoval in Mirrorlands shops (same peers, same message, same counter):
    /// pick any deck card, pay, add an exact copy, and count it like a removal so later services cost more.
    /// </summary>
    public static async Task<bool> DuplicateFromDeck(Player player, int goldCost, bool cancelable)
    {
        var prefs = new CardSelectorPrefs(ZoneTheSpireModifier.ModLoc("duplicate_prompt"), 1)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = true,
        };
        CardModel? card = (await CardSelectCmd.FromDeckGeneric(player, prefs)).FirstOrDefault();
        if (card == null)
        {
            return false;
        }

        // Copy first: if adding the copy fails, no gold is spent.
        await MirrorDuplication.AddCopyToDeck(card, player);
        await PlayerCmd.LoseGold(goldCost, player, GoldLossType.Spent);
        player.ExtraFields.CardShopRemovalsUsed++;
        return true;
    }
}
