using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Worldly Attachment (the relic's held-on life): its owner has already died once this fight and is living on. They take no
/// damage and no HP loss, their health bar is the full purple infinite bar, and the counter is the turns they have left: it
/// drops at the end of each of their own turns, so dying on the enemy's turn still buys two full turns. When it runs out they
/// die there and then (a normal death, which in co-op their allies can still come back from). Ending the fight first ends it
/// at 1 HP plus a heal instead (WorldlyAttachmentLife.OnCombatEnd). Its original icon is served by BloodDrinkerIconPatch
/// (Intangible remains the fallback).
/// </summary>
public sealed class WorldlyAttachmentPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => ModelDb.Power<IntangiblePower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<IntangiblePower>().ResolvedBigIconPath;

    /// <summary>Living on: no damage at all (previews show 0 too).</summary>
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner ? 0m : decimal.MaxValue;

    /// <summary>Living on: no HP loss either, so poison and HP costs tick away without doing anything.</summary>
    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && CombatManager.Instance.IsInProgress ? 0m : amount;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        try
        {
            if (side != CombatSide.Player || !participants.Contains(Owner) || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            int left = PilgrimRules.TurnsLeft(Amount);
            SetAmount(left);
            if (left > 0)
            {
                Flash();
                return;
            }

            // Out of turns: the attachment lets go. Forced, so it can't hold the same death twice.
            Owner.HpDisplay = HpDisplay.Normal;
            await PowerCmd.Remove<WorldlyAttachmentPower>(Owner);
            await CreatureCmd.Kill(Owner, force: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Worldly Attachment failed to end its extra turns: {ex}");
        }
    }
}
