using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Forgotten Empire: Plating for Marbled. At the end of each of its owner's turns the owner gains Marbled equal to the
/// Polishing left, and then Polishing ticks down (by one for players; by one per player for enemies, exactly like Plating, so
/// it lasts the same number of rounds however many players there are). A Forgotten Statue loses all of its Polishing when its
/// Marbled breaks (MarbledPower). Its original icon is served by BloodDrinkerIconPatch (Plating remains the fallback).
/// </summary>
public sealed class PolishingPower : CustomPowerModel
{
    private const string DecrementKey = "Decrement";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => ModelDb.Power<PlatingPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<PlatingPower>().ResolvedBigIconPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar(DecrementKey, 1m) };

    /// <summary>Enemies tick down by one per player, so Polishing lasts the same number of rounds in co-op (as Plating does).</summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Side == CombatSide.Enemy && Owner.CombatState != null)
        {
            DynamicVars[DecrementKey].BaseValue = Owner.CombatState.RunState.Players.Count;
        }

        return Task.CompletedTask;
    }

    /// <summary>Early, so the Marbled is there before end-of-turn damage.</summary>
    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Amount <= 0 || Owner.IsDead)
        {
            return;
        }

        Flash();
        MarbleSounds.Grow();
        await MarbledPower.Add(Owner, Amount, null);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.IsDead)
        {
            return;
        }

        // Skip the first round, so a statue keeps its full Polishing for its first turn.
        if (Owner.Side == CombatSide.Enemy)
        {
            if (combatState.RoundNumber == 1)
            {
                return;
            }

            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -DynamicVars[DecrementKey].BaseValue, null, null);
            return;
        }

        if (Owner.Player?.PlayerCombatState?.TurnNumber != 1)
        {
            await PowerCmd.Decrement(this);
        }
    }
}
