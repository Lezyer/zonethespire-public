using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Ferrosand: the first unblocked hits the owner takes each round deal only 1 damage, one hit per stack (one stack per player).
/// The icon counts down the hits left this turn (DisplayAmount), so it is clear when attacks hit for full damage again.
/// Charges refill when the players' turn starts; while none are left the owner stops levitating. Runs inside the synced HP loss hook (never called for previews), so every
/// peer counts the same hits. Its original icon is served by BloodDrinkerIconPatch (Black Hole remains the fallback); the levitation
/// look is local rendering only.
/// </summary>
public sealed class MagnetizedPower : CustomPowerModel
{
    private int _chargesUsed;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => ModelDb.Power<BlackHolePower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<BlackHolePower>().ResolvedBigIconPath;

    /// <summary>The icon counts down the hits left this turn, so it is clear when attacks hit for full damage again.</summary>
    public override int DisplayAmount => System.Math.Max(0, Amount - _chargesUsed);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        LevitationVisual.Attach(Owner);
        return Task.CompletedTask;
    }

    /// <summary>Charges refill when the players' turn starts (after the enemies' turn), and the owner levitates again.</summary>
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || _chargesUsed == 0)
        {
            return Task.CompletedTask;
        }

        _chargesUsed = 0;
        InvokeDisplayAmountChanged();
        if (Owner.IsAlive)
        {
            LevitationVisual.Attach(Owner);
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        int damage = (int)decimal.Floor(amount);
        if (target != Owner || !FerrosandRules.ShouldDampen(damage, _chargesUsed, (int)Amount))
        {
            return amount;
        }

        _chargesUsed++;
        InvokeDisplayAmountChanged();
        Flash();
        if (_chargesUsed >= (int)Amount)
        {
            // Out of dampened hits this turn: stop levitating until the charges refill.
            LevitationVisual.Ground(Owner);
        }

        return FerrosandRules.DampenedHpLoss(damage);
    }
}
