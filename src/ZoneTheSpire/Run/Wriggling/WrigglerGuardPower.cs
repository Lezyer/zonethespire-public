using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Infestation;

namespace ZoneTheSpire.Run.Wriggling;

/// <summary>
/// On the Wriggler pet. Attack damage aimed at its owner or the owner's Osty hits the Wriggler first (like Osty's Die For
/// You, which it takes priority over in either hook order). Damage beyond the Wriggler's HP spills into a living Osty before
/// reaching the owner. When the Wriggler dies it deals damage equal to its Max HP to all enemies and 25% of its Max HP to
/// its owner (unblockable). All state changes happen
/// inside the synced damage and death hooks, so every peer resolves them identically.
/// </summary>
public sealed class WrigglerGuardPower : CustomPowerModel
{
    private Creature? _overflowTarget;
    private Creature? _pendingOsty;
    private decimal _pendingOstyDamage;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<InfestedPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<InfestedPower>().ResolvedBigIconPath;

    /// <summary>
    /// Adding any pet for a player makes the game switch that player's other pets non-interactable (hiding the health bar,
    /// name and hover tips), which vanilla only skips for the local Osty. Switch the Wriggler back on, like Osty.
    /// </summary>
    public override Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature != Owner && creature.PetOwner != null && creature.PetOwner == Owner.PetOwner)
        {
            WrigglerSummon.ShowInterface(Owner);
        }

        return Task.CompletedTask;
    }

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)
    {
        _overflowTarget = null;
        Player? player = Owner.PetOwner;
        if (player == null || Owner.IsDead || !props.IsPoweredAttack())
        {
            return target;
        }

        Creature? osty = player.Osty;
        bool aimedAtOwner = target == player.Creature;
        bool aimedAtOsty = osty != null && osty.IsAlive && target == osty;
        if (!aimedAtOwner && !aimedAtOsty)
        {
            return target;
        }

        // The game sends the Wriggler's overkill to the hit's original target. A hit on the owner may already have been
        // redirected to Osty by Die For You (hook order depends on summon order), so the target seen here can be Osty even
        // though the overkill will go to the owner. Remember the owner either way: only an overkill aimed at the owner is
        // routed through Osty, while a direct hit on Osty sends its overkill to Osty anyway.
        _overflowTarget = player.Creature;
        return Owner;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_overflowTarget == null || target != _overflowTarget)
        {
            return amount;
        }

        _overflowTarget = null;
        Creature? osty = Owner.PetOwner?.Osty;
        if (amount <= 0m || osty == null || !osty.IsAlive)
        {
            return amount;
        }

        decimal absorbed = Math.Min(amount, osty.CurrentHp);
        _pendingOsty = osty;
        _pendingOstyDamage += absorbed;
        return amount - absorbed;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (_pendingOsty == null || _pendingOstyDamage <= 0m)
        {
            return;
        }

        Creature osty = _pendingOsty;
        decimal damage = _pendingOstyDamage;
        _pendingOsty = null;
        _pendingOstyDamage = 0m;
        // Unpowered, so no guard (the Wriggler's or Osty's) redirects it again.
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), osty, damage, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Owner || wasRemovalPrevented)
        {
            return;
        }

        List<Creature> enemies = Owner.CombatState?.Enemies.Where(enemy => enemy.IsAlive).ToList() ?? new List<Creature>();
        Creature? dealer = Owner.PetOwner?.Creature;
        if (dealer == null || dealer.IsDead)
        {
            return;
        }

        int ownerDamage = WrigglingRules.OwnerDeathDamage(Owner.MaxHp);
        if (enemies.Count > 0)
        {
            await CreatureCmd.Damage(choiceContext, enemies, Owner.MaxHp, ValueProp.Unpowered, dealer, null, null);
        }

        if (ownerDamage > 0)
        {
            // Unblockable and unpowered: the backlash is a cost, so nothing redirects or blocks it.
            await CreatureCmd.Damage(choiceContext, dealer, ownerDamage, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }
    }
}
