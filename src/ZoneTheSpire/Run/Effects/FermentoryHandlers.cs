using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Run.Campfire;
using ZoneTheSpire.Run.Fermentory;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// The Fermentory fights: enemies take turns brewing a potion (EnemyBrewing), and each player gets one extra reward: a potion
/// slot, a potion or a special potion slot (FermentoryRules.PickFightReward, seeded per location and player).
/// </summary>
internal sealed class FermentoryFightsHandler : ZoneEffectHandler
{
    public override string EffectId => FermentoryBiome.FightsEffectId;

    public override Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        try
        {
            IRunState runState = room.CombatState.RunState;
            string location = SpecialSlots.CurrentLocation(runState);
            foreach (Player player in room.CombatState.Players)
            {
                FightReward pick = FermentoryRules.PickFightReward(runState.Rng.Seed, location, player.NetId, SpecialSlots.CanGain(player));
                Reward reward = pick switch
                {
                    FightReward.PotionSlot => new PotionSlotReward(player),
                    FightReward.SpecialSlot => new SpecialSlotReward(player),
                    _ => new PotionReward(player),
                };
                room.AddExtraReward(player, reward);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Fermentory fight reward: {ex}");
        }

        return Task.CompletedTask;
    }

    public override Task OnAfterPlayerTurnStart(Player player, ZoneContext context)
    {
        try
        {
            if (player.Creature.CombatState is { } combat)
            {
                EnemyBrewing.BrewForRound(combat, player.RunState);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Fermentory brewing failed: {ex}");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The Fermentory shops: 6 potions for sale (FermentoryShopPotionsPatch) and the card removal slot sells a Bottle (+1 potion
/// slot) for a flat 75 Gold (BottleShop).
/// </summary>
internal sealed class FermentoryShopHandler : ZoneEffectHandler
{
    public override string EffectId => FermentoryBiome.ShopEffectId;

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) =>
        entry is MerchantCardRemovalEntry ? FermentoryRules.BottlePrice : cost;
}

/// <summary>The Fermentory campfires: an extra Distil option.</summary>
internal sealed class FermentoryDistilHandler : ZoneEffectHandler
{
    public override string EffectId => FermentoryBiome.DistilEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        options.Add(new DistilRestSiteOption(player));
        return true;
    }
}
