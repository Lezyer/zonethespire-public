using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Phantasmal Tombs (Monster, Elite, "?" fights): every enemy is a Phantasm (including enemies added during the fight, but not
/// ghostly copies); dying Phantasms rise as ghostly copies or, for reviving enemies, rise once at 20% HP; one card in each
/// card reward is Phantasm-Haunted. All inside synced hooks, with picks from the run seed and map location.
/// </summary>
internal sealed class PhantasmalTombsHandler : ZoneEffectHandler
{
    public override string EffectId => PhantasmalTombsBiome.PhantasmsEffectId;

    /// <summary>
    /// Each player's rewards for this fight have a 50% chance to include a Ghost in a Jar (added like any extra reward, on every
    /// peer; the roll uses the run seed, the map location and the player, so every peer agrees).
    /// </summary>
    public override Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        try
        {
            IRunState runState = room.CombatState.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            foreach (Player player in room.CombatState.Players)
            {
                if (!PhantasmalRules.DropsGhostInAJar(runState.Rng.Seed, location, player.NetId))
                {
                    continue;
                }

                room.AddExtraReward(player, new PotionReward(ModelDb.Potion<GhostInAJar>().ToMutable(), player));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Phantasmal Tombs Ghost in a Jar reward: {ex}");
        }

        return Task.CompletedTask;
    }

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await PhantasmSpectres.GivePhantasm(enemy);
        }
    }

    /// <summary>Enemies summoned or spawned mid-fight. Ghostly copies are marked before they are added, so they are skipped.</summary>
    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await PhantasmSpectres.GivePhantasm(creature);
        }
    }

    public override Task OnBeforeDeath(Creature creature, ZoneContext context)
    {
        PhantasmSpectres.RememberDeathPosition(creature);
        return Task.CompletedTask;
    }

    public override async Task OnAfterDeath(Creature creature, bool wasRemovalPrevented, float deathAnimLength, ZoneContext context)
    {
        if (!wasRemovalPrevented)
        {
            await PhantasmSpectres.HandleDeath(creature);
        }
    }

    public override bool TryModifyCardReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions, ZoneContext context)
    {
        if (creationOptions.Source != CardCreationSource.Encounter
            || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications))
        {
            return false;
        }

        try
        {
            // CardReward.Populate can re-invoke this hook for pre-set rewards: leave rewards that already hold a haunted card alone.
            if (options.Any(option => PhantasmHauntedModifier.IsHaunted(option.Card)))
            {
                return false;
            }

            List<CardModel> eligible = options.Select(option => option.Card).Where(PhantasmHauntedModifier.CanHaunt).ToList();
            IRunState runState = player.RunState;
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            string optionsKey = string.Join(",", options.Select(option => option.Card.Id.Entry));
            int index = PhantasmalRules.PickRewardHauntIndex(runState.Rng.Seed, location, player.NetId, optionsKey, eligible.Count);
            if (index < 0 || !PhantasmHauntedModifier.TryAdd(eligible[index]))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to haunt a Phantasmal Tombs card reward: {ex}");
            return false;
        }
    }
}
