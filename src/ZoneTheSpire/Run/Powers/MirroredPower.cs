using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Mirrorlands: the first time the owner is at or below half HP, it spawns a mirrored clone. The power's amount is the
/// clone's HP percent of the owner's Max HP (25 or 50).
/// </summary>
public sealed class MirroredPower : CustomPowerModel
{
    private bool _hasSplit;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<ReflectPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<ReflectPower>().ResolvedBigIconPath;

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

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || delta >= 0m || !MirrorRules.ShouldSplit(Owner.IsAlive, Owner.CurrentHp, Owner.MaxHp, _hasSplit))
        {
            return;
        }

        _hasSplit = true;
        try
        {
            int clonePercent = Amount > 0 ? (int)Math.Min(Amount, 100) : MirrorRules.AloneCloneHpPercent;
            await MirrorCloning.SpawnClone(Owner, clonePercent);
            Flash();
        }
        catch (Exception ex)
        {
            Log.Warn($"Mirrored split failed: {ex}");
        }
    }
}
