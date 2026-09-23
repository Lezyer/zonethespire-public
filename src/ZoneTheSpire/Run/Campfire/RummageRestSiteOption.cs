using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Scrapyard;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Scrapyard Rummage: independent chances at 25–80 gold (reduced on the Poverty ascension), a potion, a card reward, a rare
/// card reward and a relic. Which rewards appear uses the run seed, map location and the player's id: different per player,
/// identical on every peer. The reward contents (including the gold amount) come from that player's synced reward RNG, so
/// peers agree on them too.
/// </summary>
public sealed class RummageRestSiteOption : RestSiteOption
{
    private const int MinGold = 25;
    private const int MaxGold = 80;

    private readonly HealRestSiteOption _vanillaAssets;

    public RummageRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.RummageId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override async Task<bool> OnSelect()
    {
        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
        RummageRolls rolls = ScrapyardRules.RollRummage(runState.Rng.Seed, location, Owner.NetId);

        var rewards = new List<Reward>();
        if (rolls.Gold)
        {
            // GoldReward rolls the amount (inclusive) from Player.PlayerRng.Rewards when populated: synced per player.
            // Poverty reduces it like vanilla fight gold (x0.75, rounded down).
            rewards.Add(new GoldReward(WithPoverty(MinGold), WithPoverty(MaxGold), Owner));
        }

        if (rolls.Potion)
        {
            rewards.Add(new PotionReward(Owner));
        }

        if (rolls.Card)
        {
            rewards.Add(new CardReward(CardCreationOptions.ForRoom(Owner, RoomType.Monster), 3, Owner));
        }

        if (rolls.RareCard)
        {
            // Encounter rarity odds must not be combined with a rarity-limited pool, so use uniform odds over rare cards.
            CardCreationOptions rareOptions = CardCreationOptions.ForRoom(Owner, RoomType.Monster)
                .WithFilter(card => card.Rarity == CardRarity.Rare)
                .WithRarityOdds(CardRarityOddsType.Uniform);
            rewards.Add(new CardReward(rareOptions, 3, Owner));
        }

        if (rolls.Relic)
        {
            rewards.Add(new RelicReward(Owner));
        }

        if (rewards.Count > 0)
        {
            await RewardsCmd.OfferCustom(Owner, rewards);
        }

        return true;
    }

    /// <summary>Applies the Poverty ascension gold multiplier the same way EncounterModel does for fight gold.</summary>
    private static int WithPoverty(int amount) =>
        AscensionHelper.HasAscension(AscensionLevel.Poverty) ? (int)(amount * AscensionHelper.PovertyAscensionGoldMultiplier) : amount;
}
