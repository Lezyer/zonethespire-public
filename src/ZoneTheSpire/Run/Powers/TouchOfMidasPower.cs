using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using System;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Halls of Midas: the owner gains 1 Strength at the end of each of its turns, and whenever it deals unblocked damage to a
/// player, that player gains 3 gold per point of that damage. Both run inside synced combat hooks, so every peer agrees. Its icon is served by
/// BloodDrinkerIconPatch (Strength remains the fallback); the golden sheen is local rendering only.
/// </summary>
public sealed class TouchOfMidasPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new IHoverTip[] { HoverTipFactory.FromPower<StrengthPower>() };

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GoldMaterial.Apply(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, HallsOfMidasRules.StrengthPerTurn, Owner, null);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target.Player is not Player player)
        {
            return;
        }

        int gold = HallsOfMidasRules.GoldForDamage(result.UnblockedDamage);
        if (gold > 0)
        {
            await PlayerCmd.GainGold(gold, player);
        }
    }
}
