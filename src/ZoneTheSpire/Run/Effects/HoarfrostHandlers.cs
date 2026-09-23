using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Run.Hoarfrost;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Hoarfrost (Monster, Elite, "?" fights): every player gets Hoarfrost, which freezes 2 cards of their hand each turn (see
/// FrozenCards), and every card they are offered afterwards carries Biting Cold. Runs in synced combat hooks.
/// </summary>
internal sealed class HoarfrostFightsHandler : ZoneEffectHandler
{
    public override string EffectId => HoarfrostBiome.FrozenHandEffectId;

    /// <summary>Every card of a fight's reward carries Biting Cold, 3 per energy of its cost.</summary>
    public override bool TryModifyCardReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions, ZoneContext context)
    {
        if (creationOptions.Source != CardCreationSource.Encounter
            || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications))
        {
            return false;
        }

        bool modified = false;
        foreach (CardModel card in options.Select(option => option.Card))
        {
            // CardReward.Populate can re-invoke this hook for pre-set rewards: TryAdd never adds Biting Cold twice.
            modified |= BitingColdModifier.TryAdd(card, FrostRules.RewardCold(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }

        return modified;
    }

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Player player in room.CombatState.Players)
        {
            try
            {
                if (player.Creature is { IsDead: false } creature && !creature.HasPower<HoarfrostPower>())
                {
                    await PowerCmd.Apply<HoarfrostPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to give player {player.NetId} Hoarfrost: {ex}");
            }
        }
    }
}

/// <summary>Hoarfrost shops: every card for sale carries Biting Cold, 3 per energy of its cost.</summary>
internal sealed class HoarfrostShopHandler : ZoneEffectHandler
{
    public override string EffectId => HoarfrostBiome.FrostShopEffectId;

    public override void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
        foreach (CardModel card in cards.Select(entry => entry.Card))
        {
            BitingColdModifier.TryAdd(card, FrostRules.ShopCold(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }
    }
}

/// <summary>Hoarfrost campfires: Frostbind is added beside the usual options.</summary>
internal sealed class HoarfrostCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => HoarfrostBiome.FrostbindEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        options.Add(new Campfire.FrostbindRestSiteOption(player));
        return true;
    }
}
