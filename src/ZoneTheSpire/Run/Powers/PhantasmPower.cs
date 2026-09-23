using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Phantasmal Tombs: the owner loses 25% less HP from unblocked damage (rounded down, at least 1) and its attacks deal 25% less
/// damage until it rises again (a revived enemy deals full damage; ghostly copies have no Phantasm). Runs inside the synced
/// damage and HP loss hooks, so every peer agrees. Kept after death so enemies that revive stay Phantasms. Its icon is served by
/// BloodDrinkerIconPatch (Haunt remains the fallback); the ghostly look is local rendering only.
/// </summary>
public sealed class PhantasmPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<HauntPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<HauntPower>().ResolvedBigIconPath;

    /// <summary>Set once the owner has risen again; from then on its attacks deal full damage.</summary>
    private bool _risen;

    internal void MarkRisen() => _risen = true;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer != Owner ? 1m : PhantasmalRules.DealtDamageMultiplier(props.IsPoweredAttack(), _risen);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GhostMaterial.Apply(Owner, GhostStrength.Phantasm);
        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m)
        {
            return amount;
        }

        return PhantasmalRules.ReducedHpLoss((int)decimal.Floor(amount));
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}
