using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Prismatic Storm Stare at Prism: a card reward of 3 upgraded colourless cards. The cards come from the player's synced
/// reward RNG (as vanilla rewards do), so every peer offers the same cards.
/// </summary>
public sealed class StareAtPrismRestSiteOption : RestSiteOption
{
    private const int CardCount = 3;

    private readonly HealRestSiteOption _vanillaAssets;

    public StareAtPrismRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.StareAtPrismId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override async Task<bool> OnSelect()
    {
        // Uniform odds: the colourless pool has no commons, so encounter rarity odds don't fit it.
        CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(new[] { ModelDb.CardPool<ColorlessCardPool>() });
        List<CardModel> cards = CardFactory.CreateForReward(Owner, CardCount, options).Select(result => result.Card).ToList();
        CardCmd.Upgrade(cards, CardPreviewStyle.None);

        var reward = new CardReward(cards, CardCreationSource.Other, Owner, options);
        // A reroll (e.g. Driftwood) regenerates the cards from the reroll options, which don't upgrade; AfterGenerated fires
        // after the new cards are made and before the screen refreshes, so upgrade them there too.
        reward.AfterGenerated += () => CardCmd.Upgrade(reward.Cards.ToList(), CardPreviewStyle.None);
        await RewardsCmd.OfferCustom(Owner, new List<Reward> { reward });
        return true;
    }
}
