using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Hoarfrost;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Hoarfrost: every attack against the iced creature deals its Biting Cold in extra damage, and each hit that lands takes 1 off.
/// It is usually on an enemy (cards with Biting Cold ice what they hit), but Frostheart also ices its own owner.
/// The bonus is added where Strength is, so Vulnerable and the like still multiply it, and it shows in damage previews; only a
/// real hit spends it, and a multi-hit attack spends one per hit (so it deals a little less each time). It is one shared pool on
/// the enemy: in co-op anyone's attacks spend it and anyone's cards add to it. A debuff, so Artifact blocks it. Its icon is
/// served by BloodDrinkerIconPatch.
/// </summary>
public sealed class BitingColdPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => ModelDb.Power<FrailPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<FrailPower>().ResolvedBigIconPath;

    /// <summary>The extra damage, added alongside Strength so later multipliers still apply to it (previews included).</summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && props.IsPoweredAttack() ? FrostRules.DamageBonus(Amount) : 0m;

    /// <summary>A hit that landed (blocked or not) takes 1 off; multi-hit attacks come through here once per hit.</summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        try
        {
            if (target == Owner && props.IsPoweredAttack() && Amount > 0)
            {
                SetAmount(FrostRules.ColdAfterHit(Amount));
                Flash();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Biting Cold failed to thaw after a hit: {ex}");
        }

        return Task.CompletedTask;
    }
}
