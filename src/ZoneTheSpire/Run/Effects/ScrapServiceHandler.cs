using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Scrapyard shops: the card removal slot becomes Scrap and is free (the removal price counter is untouched). The slot is
/// never marked unavailable up front (that shows it as sold out); instead the purchase flow in <see cref="ScrapShop"/> does
/// nothing when the player has no upgraded card.
/// </summary>
internal sealed class ScrapServiceHandler : ZoneEffectHandler
{
    public override string EffectId => ScrapyardBiome.ScrapServiceEffectId;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) =>
        entry is MerchantCardRemovalEntry ? 0m : cost;
}
