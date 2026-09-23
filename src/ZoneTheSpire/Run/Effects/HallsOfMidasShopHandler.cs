using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Midas;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Halls of Midas shops: every card for sale (including restocks) is upgraded, and card prices are 30% higher (applied after
/// sales). Shop stock is generated identically on every peer, so upgrades and prices match everywhere; a bought card stays
/// upgraded. Relics, potions and card removal keep their prices.
/// </summary>
internal sealed class HallsOfMidasShopHandler : ZoneEffectHandler
{
    private static readonly object Marker = new();

    /// <summary>Cards this shop already upgraded: the shop re-invokes the hook when refreshing an entry.</summary>
    private static readonly ConditionalWeakTable<CardModel, object> Gilded = new();

    public override string EffectId => HallsOfMidasBiome.GildedShopEffectId;

    public override void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
        foreach (CardCreationResult result in cards)
        {
            CardModel card = result.Card;
            if (Gilded.TryGetValue(card, out _))
            {
                continue;
            }

            Gilded.Add(card, Marker);
            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card, CardPreviewStyle.None);
            }
        }
    }

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) =>
        entry is MerchantCardEntry ? HallsOfMidasRules.CardPrice(cost) : cost;
}
