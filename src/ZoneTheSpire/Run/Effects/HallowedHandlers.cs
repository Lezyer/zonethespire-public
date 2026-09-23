using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Campfire;
using ZoneTheSpire.Run.Hallowed;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Blinding Hallows fights: every player gets Blasphemer, every enemy (mid-fight adds too) gets Zealous.</summary>
internal sealed class HallowedFightsHandler : ZoneEffectHandler
{
    public override string EffectId => HallowedBiome.FightsEffectId;

    /// <summary>
    /// A fight's card reward: every attack carries Hallowing, every other playable card Redemption (curses and statuses stay
    /// as they are).
    /// </summary>
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
            // CardReward.Populate can re-invoke this hook for pre-set rewards: TryAdd never adds a modifier twice.
            modified |= card.Type == CardType.Attack ? HallowingModifier.TryAdd(card) : RedemptionModifier.TryAdd(card);
        }

        return modified;
    }

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Player player in room.CombatState.Players)
        {
            try
            {
                if (player.Creature is { IsDead: false } creature && !creature.HasPower<BlasphemerPower>())
                {
                    await PowerCmd.Apply<BlasphemerPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to give player {player.NetId} Blasphemer: {ex}");
            }
        }

        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await Zealous(enemy);
        }
    }

    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await Zealous(creature);
        }
    }

    private static async Task Zealous(Creature creature)
    {
        try
        {
            if (creature.Side == CombatSide.Enemy && creature.IsMonster && creature.IsAlive && creature.CombatState != null && !creature.HasPower<ZealousPower>())
            {
                await PowerCmd.Apply<ZealousPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to make {creature.LogName} Zealous: {ex}");
        }
    }
}

/// <summary>Blinding Hallows shops: up to 3 random attacks for sale gain Hallowing (fewer when fewer attacks are sold).</summary>
internal sealed class HallowedShopHandler : ZoneEffectHandler
{
    public override string EffectId => HallowedBiome.ShopEffectId;

    public override void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
        try
        {
            List<CardModel> attacks = cards.Select(entry => entry.Card).Where(HallowingModifier.CanHallow).ToList();
            IRunState runState = player.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            foreach (int index in HallowedRules.PickIndices(runState.Rng.Seed, HallowedRules.ShopStream(location), player.NetId, attacks.Count, HallowedRules.ShopHallowingCount))
            {
                HallowingModifier.TryAdd(attacks[index]);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Blinding Hallows failed to hallow shop cards: {ex}");
        }
    }
}

/// <summary>Blinding Hallows campfires: an extra Repent option.</summary>
internal sealed class HallowedRepentHandler : ZoneEffectHandler
{
    public override string EffectId => HallowedBiome.RepentEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        options.Add(new RepentRestSiteOption(player));
        return true;
    }
}

/// <summary>Blinding Hallows campfires: Smith becomes Blaspheme (ShadowCampfireHandler's list replacement).</summary>
internal sealed class HallowedBlasphemeHandler : ZoneEffectHandler
{
    public override string EffectId => HallowedBiome.BlasphemeEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context)
    {
        if (options is not IList<RestSiteOption> list)
        {
            Log.Warn("Rest site options are not an indexable list; Blaspheme skipped.");
            return false;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is SmithRestSiteOption smith)
            {
                list[i] = new BlasphemeRestSiteOption(player, smith.SmithCount);
                return true;
            }
        }

        return false;
    }
}
