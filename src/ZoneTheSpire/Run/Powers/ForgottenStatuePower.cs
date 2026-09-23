using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ForgottenEmpire;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Forgotten Empire: marks an enemy as a Forgotten Statue. While it still has Marbled (granted alongside it by
/// ForgottenEmpireHandler; hidden from the power bar and shown as a white shield on the health bar) it takes 25% less damage.
/// It is removed when the Marbled breaks. The multiplier runs in the synced damage hooks, so intents and card previews show it
/// too. Its original icon is served by BloodDrinkerIconPatch (Rolling Boulder remains the fallback).
/// </summary>
public sealed class ForgottenStatuePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<RollingBoulderPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<RollingBoulderPower>().ResolvedBigIconPath;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }

        int marbled = Owner.GetPower<MarbledPower>()?.Amount ?? 0;
        return ForgottenEmpireRules.StatueDamageMultiplier(marbled, props.HasFlag(ValueProp.Unblockable));
    }
}
