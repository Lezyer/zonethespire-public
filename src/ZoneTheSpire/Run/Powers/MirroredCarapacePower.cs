using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Mirrorlands marker on Decimillipede segments, which are never mirrored and get bonus Max HP instead. The HP itself is
/// set by the Mirrorlands handler; this power only explains it. Kept through segment deaths so it survives Reattach.
/// </summary>
public sealed class MirroredCarapacePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<ReflectPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<ReflectPower>().ResolvedBigIconPath;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        MirrorMaterial.Apply(Owner, MirrorStrength.Subtle);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        MirrorMaterial.Remove(oldOwner);
        return Task.CompletedTask;
    }
}
