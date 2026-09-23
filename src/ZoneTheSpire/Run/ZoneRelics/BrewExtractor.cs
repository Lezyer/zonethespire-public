using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Fermentory zone relic. Card reward screens gain an Extract option (like vanilla Pael's Wing's Sacrifice): taking it
/// gives up the card reward for a random potion, rolled from the owner's synced reward RNG. With a free potion slot the potion
/// goes straight into it; with a full belt it comes as a potion reward (discard a potion to take it, or skip it). Offered
/// whenever the screen has room for another option (ZoneRelicEffects.BrewExtractorOffers). An Event relic, so it only comes
/// from Fermentory chests.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class BrewExtractor : ModArtRelicModel
{
    protected override string TextureName => "brew_extractor";

    protected override string IconBaseName => "potion_belt";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool TryModifyCardRewardAlternatives(Player player, CardReward cardReward, List<CardRewardAlternative> alternatives)
    {
        if (Owner != player || !ZoneRelicEffects.BrewExtractorOffers(alternatives.Count))
        {
            return false;
        }

        RestSiteLoc.EnsureInjected();
        alternatives.Add(new CardRewardAlternative(RestSiteLoc.ExtractAlternativeId, Extract, PostAlternateCardRewardAction.EndSelectionAndCompleteReward));
        return true;
    }

    private async Task Extract()
    {
        try
        {
            Flash();
            if (!Owner.HasOpenPotionSlots)
            {
                // No room in the belt: the potion comes as a reward, so a potion can be discarded to make room for it.
                await RewardsCmd.OfferCustom(Owner, new List<Reward> { new PotionReward(Owner) });
                return;
            }

            PotionModel potion = PotionFactory.CreateRandomPotionOutOfCombat(Owner, Owner.PlayerRng.Rewards).ToMutable();
            await PotionCmd.TryToProcure(potion, Owner);
        }
        catch (Exception ex)
        {
            Log.Warn($"Brew Extractor failed to extract a potion: {ex}");
        }
    }
}
