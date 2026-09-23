using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Mirrorlands zone relic: the first enemy hit each combat that removes all of the owner's Block loses none of its damage to
/// that Block—the whole post-modifier hit is dealt back to the attacker—while the owner's HP loss is reduced to zero. The
/// incoming amount is captured only in BeforeDamageReceived, so previews cannot arm the relic. All state and retaliation run
/// inside synchronized damage hooks and CreatureCmd.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class ReflectiveShard : ModArtRelicModel
{
    protected override string TextureName => "reflective_shard";

    private bool _usedThisCombat;
    private Creature? _pendingDealer;
    private int _pendingDamage;

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "data_disk";

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner.Creature)
        {
            return Task.CompletedTask;
        }

        _pendingDealer = null;
        _pendingDamage = 0;
        int incoming = Math.Max(0, (int)decimal.Floor(amount));
        if (dealer != null && ZoneRelicEffects.ReflectiveShardTriggers(
                _usedThisCombat,
                target.Block,
                incoming,
                props.HasFlag(ValueProp.Unblockable),
                dealer.IsEnemy))
        {
            _pendingDealer = dealer;
            _pendingDamage = incoming;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // The target may be an owner-controlled damage redirect such as Osty. The shard cancels the entire breaking hit.
        return !_usedThisCombat
            && _pendingDamage > 0
            && _pendingDealer == dealer
            && Owner.Creature.Block <= 0
            ? 0m
            : amount;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner.Creature)
        {
            return;
        }

        Creature? attacker = _pendingDealer;
        int reflected = _pendingDamage;
        _pendingDealer = null;
        _pendingDamage = 0;
        if (_usedThisCombat || attacker == null || attacker != dealer || !result.WasBlockBroken || reflected <= 0)
        {
            return;
        }

        _usedThisCombat = true;
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            attacker,
            reflected,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }

    private void ResetCombatState()
    {
        _usedThisCombat = false;
        _pendingDealer = null;
        _pendingDamage = 0;
    }
}
