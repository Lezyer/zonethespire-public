using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Infestation shops: every card for sale (including restocks) gets Wriggling 2 x its cost (0-cost and X-cost count as 1).
/// Shop stock is generated identically on every peer, so the modifiers match everywhere; a bought card keeps it.
/// </summary>
internal sealed class InfestationShopHandler : ZoneEffectHandler
{
    public override string EffectId => InfestationBiome.ShopWrigglingEffectId;

    public override void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
        foreach (CardCreationResult result in cards)
        {
            CardModel card = result.Card;
            // The shop re-invokes this hook when refreshing an entry: TryAdd never adds Wriggling twice.
            WrigglingModifier.TryAdd(card, WrigglingRules.ShopAmount(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }
    }
}
