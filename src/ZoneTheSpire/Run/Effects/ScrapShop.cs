using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

internal static class ScrapShop
{
    public static bool IsScrapActive(Player player) =>
        ZoneEffectQuery.IsActive(player.RunState, ScrapyardBiome.ScrapServiceEffectId);

    /// <summary>
    /// Replaces OneOffSynchronizer.DoMerchantCardRemoval in Scrapyard shops (runs for the buyer and, via the vanilla removal
    /// message, every other peer): pick an upgraded card, downgrade it, and offer two skippable card rewards. No gold is
    /// spent and the card removal price counter is not increased.
    /// </summary>
    public static async Task<bool> ScrapFromDeck(Player player, bool cancelable)
    {
        // Scrap needs an upgraded card. Every peer runs this same check, so the slot simply stays unused.
        if (!player.Deck.Cards.Any(candidate => candidate.IsUpgraded))
        {
            return false;
        }

        var prefs = new CardSelectorPrefs(ZoneTheSpireModifier.ModLoc("scrap_prompt"), 1)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = true,
        };
        CardModel? card = (await CardSelectCmd.FromDeckGeneric(player, prefs, candidate => candidate.IsUpgraded)).FirstOrDefault();
        if (card == null)
        {
            return false;
        }

        CardCmd.Downgrade(card);
        var rewards = new List<Reward>
        {
            new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster), 3, player),
            new CardReward(CardCreationOptions.ForRoom(player, RoomType.Monster), 3, player),
        };
        await RewardsCmd.OfferCustom(player, rewards);
        return true;
    }
}
