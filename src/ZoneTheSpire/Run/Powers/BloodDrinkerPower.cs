using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Blood Rain: the owner heals for 70% of the unblocked damage it deals to any creature but itself (every player in
/// multiplayer, their pets included; rounded, at least 1). Runs inside the synced damage hook, so every peer heals
/// identically. Its original loose icon is served by BloodDrinkerIconPatch (Burst remains the fallback).
/// </summary>
public sealed class BloodDrinkerPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<BurstPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<BurstPower>().ResolvedBigIconPath;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        CrimsonMist.Attach(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target == Owner || Owner.IsDead)
        {
            return;
        }

        int heal = BloodRainRules.HealAmount(result.UnblockedDamage, BloodRainRules.HealPercent);
        if (heal > 0)
        {
            await CreatureCmd.Heal(Owner, heal);
        }
    }
}
