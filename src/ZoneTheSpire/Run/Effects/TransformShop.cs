using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Prismatic Storm shop Transform service. Slot text and visuals are shared with other services in <see cref="ShopServices"/>.</summary>
internal static class TransformShop
{
    public static bool IsTransformActive(Player player) =>
        ZoneEffectQuery.IsActive(player.RunState, PrismaticStormBiome.TransformServiceEffectId);

    /// <summary>
    /// Replaces OneOffSynchronizer.DoMerchantCardRemoval in Prismatic Storm shops (same peers, same message, same counter):
    /// pick a transformable deck card, transform it with the player's own synced transformation RNG, pay, and count it like
    /// a removal so later services cost more.
    /// </summary>
    public static async Task<bool> TransformFromDeck(Player player, int goldCost, bool cancelable)
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = true,
        };
        CardModel? card = (await CardSelectCmd.FromDeckForTransformation(player, prefs)).FirstOrDefault();
        if (card == null)
        {
            return false;
        }

        // Transform first: if it fails, no gold is spent.
        await CardCmd.TransformToRandom(card, player.PlayerRng.Transformations, CardPreviewStyle.HorizontalLayout);
        await PlayerCmd.LoseGold(goldCost, player, GoldLossType.Spent);
        player.ExtraFields.CardShopRemovalsUsed++;
        return true;
    }
}
