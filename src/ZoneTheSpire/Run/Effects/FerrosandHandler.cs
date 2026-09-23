using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Run.Ferrosand;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Ferrosand (Monster, Elite, "?" fights): every enemy (including enemies added during the fight) is Magnetized (one stack per
/// player) and Ferroform (the Phantasmal Gardeners only get Ferroform, they float on their own); every playable card in the
/// fight's card rewards is Magnetic, and every player gets a Magnetize reward (magnetizes 1 random card in the deck). All
/// inside synced hooks.
/// </summary>
internal sealed class FerrosandHandler : ZoneEffectHandler
{
    public override string EffectId => FerrosandBiome.MagneticFoesEffectId;

    /// <summary>Every player's rewards for this fight include a Magnetize reward (added like any extra reward, on every peer).</summary>
    public override Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        try
        {
            foreach (Player player in room.CombatState.Players)
            {
                room.AddExtraReward(player, new MagnetizeReward(player));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Ferrosand Magnetize reward: {ex}");
        }

        return Task.CompletedTask;
    }

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await Magnetize(enemy);
        }
    }

    /// <summary>Enemies summoned or spawned mid-fight. The fight's starting enemies get the powers in OnBeforeCombatStart.</summary>
    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await Magnetize(creature);
        }
    }

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
            // CardReward.Populate can re-invoke this hook for pre-set rewards: TryAdd never adds Magnetic twice.
            modified |= MagneticModifier.TryAdd(card);
        }

        return modified;
    }

    private static async Task Magnetize(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || creature.CombatState == null)
        {
            return;
        }

        // The Phantasmal Gardeners float on their own (their Skittish animation conflicts with the Magnetized hover): Ferroform only.
        if (!creature.HasPower<MagnetizedPower>() && creature.Monster is not PhantasmalGardener)
        {
            int amount = FerrosandRules.MagnetizedAmount(creature.CombatState.Players.Count);
            await PowerCmd.Apply<MagnetizedPower>(new ThrowingPlayerChoiceContext(), creature, amount, null, null);
        }

        if (!creature.HasPower<FerroformPower>())
        {
            await PowerCmd.Apply<FerroformPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
        }
    }
}
