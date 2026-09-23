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
using ZoneTheSpire.Core.Ferrosand;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Ferrosand: whenever the owner's damage removes block from a player (or their pets), the owner gains a quarter of that much
/// block (rounded down, at least 1). Each
/// hit on each player counts separately, so in co-op block broken on several players adds up. Runs inside the synced damage
/// hook. Its original loose icon is served by BloodDrinkerIconPatch (Panache's icon remains the fallback).
/// </summary>
public sealed class FerroformPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<PanachePower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<PanachePower>().ResolvedBigIconPath;

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target.Side == Owner.Side || Owner.IsDead)
        {
            return;
        }

        int block = FerrosandRules.FerroformBlock(result.BlockedDamage);
        if (block > 0)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
        }
    }
}
