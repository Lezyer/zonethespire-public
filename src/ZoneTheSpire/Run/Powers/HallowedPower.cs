using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Hallowed (Blinding Hallows): Doom with a holy theme. At the end of its side's turn, a creature whose HP is at or below its
/// Hallowed dies (enemies before the enemy turn ends, players after their own turn ends, exactly where Doom kills). An enemy
/// that also has Doom first turns half its Hallowed (rounded up, at least 1) into Doom, in the VeryEarly phase, so Doom's own
/// check sees the boosted amount. A visible Debuff, so Artifact blocks it like Doom. Its icon is served by
/// BloodDrinkerIconPatch (Doom's icon is the fallback).
/// </summary>
public sealed class HallowedPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => ModelDb.Power<DoomPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<DoomPower>().ResolvedBigIconPath;

    /// <summary>
    /// The conversion, on the enemy side's turn end. Every Hallowed power gets this hook and each converts only its own owner.
    /// The VeryEarly pass finishes for every listener before the normal pass, where Doom kills.
    /// </summary>
    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || CombatManager.Instance.IsOverOrEnding
            || Owner == null || Owner.IsDead || !participants.Contains(Owner)
            || Owner.GetPower<DoomPower>() is not { } doom)
        {
            return;
        }

        int converted = HallowedRules.ConvertToDoom(Amount);
        if (converted <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, doom, converted, Owner, null);
        await PowerCmd.ModifyAmount(choiceContext, this, -converted, Owner, null);
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player && ShouldTrigger(participants))
        {
            await Judge(Condemned(Owner.CombatState!.GetCreaturesOnSide(side)));
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy && ShouldTrigger(participants))
        {
            await Judge(Condemned(Owner.CombatState!.GetCreaturesOnSide(side)));
        }
    }

    /// <summary>The living creatures that Hallowed kills now.</summary>
    internal static IReadOnlyList<Creature> Condemned(IEnumerable<Creature> creatures) =>
        creatures.Where(c => c.IsAlive && c.GetPower<HallowedPower>() is { } h && HallowedRules.IsHallowedKill(c.CurrentHp, h.Amount)).ToList();

    /// <summary>
    /// Kills the condemned together, like DoomKill, so fatal triggers (Reattach and the like) see them as one batch. Each gets
    /// the Judgement effect first (staggered). A monster that really leaves the fight (Doom's own rule: it should die and would
    /// disappear from Doom) dissolves, and that effect replaces its normal death animation. When the side has nobody else left
    /// (the fight is about to end) the whole effect plays out before the kill, as Doom waits for its last victim.
    /// </summary>
    internal static async Task Judge(IReadOnlyList<Creature> creatures)
    {
        if (creatures.Count == 0)
        {
            return;
        }

        ICombatState? combatState = creatures[0].CombatState;
        bool sideWipedOut = combatState != null
            && combatState.GetTeammatesOf(creatures[0]).Count(c => c.IsAlive) <= creatures.Count;
        for (int i = 0; i < creatures.Count; i++)
        {
            Creature creature = creatures[i];
            bool disappears = creature.IsMonster
                && creature.Monster is { ShouldDisappearFromDoom: true }
                && Hook.ShouldDie(creature.Player?.RunState ?? creature.CombatState!.RunState, creature.CombatState!, creature, out AbstractModel? _);
            // Not awaited: the game waits on it through the creature's DeathAnimationTask; the synced waits below pace the batch.
            _ = HolyVisuals.Judgement(creature, disappears);
            if (i < creatures.Count - 1)
            {
                await Cmd.Wait(0.2f);
            }
        }

        // The impact lands 0.75 s in; a last victim gets the full dissolve before the fight can end.
        await Cmd.Wait(sideWipedOut ? 2.0f : 0.9f);
        await CreatureCmd.Kill(creatures.ToList());
    }

    /// <summary>The gold segment follows Hallowed, and Doom (whose lethal state hides it), on this creature only.</summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner == Owner && power is HallowedPower or DoomPower)
        {
            HallowedHealthBar.Refresh(Owner);
        }

        return Task.CompletedTask;
    }

    /// <summary>Removed outright (a cleanse skips the amount-changed hook): clear the gold segment.</summary>
    public override Task AfterRemoved(Creature oldOwner)
    {
        HallowedHealthBar.Refresh(oldOwner);
        return Task.CompletedTask;
    }

    /// <summary>DoomPower's guards: the side's turn ends, the owner is a live, condemned participant, and the first such creature.</summary>
    private bool ShouldTrigger(IEnumerable<Creature> participants)
    {
        if (CombatManager.Instance.IsOverOrEnding || Owner?.CombatState == null || Owner.IsDead
            || !participants.Contains(Owner) || !HallowedRules.IsHallowedKill(Owner.CurrentHp, Amount))
        {
            return false;
        }

        return Condemned(Owner.CombatState.GetCreaturesOnSide(Owner.Side)).FirstOrDefault() == Owner;
    }
}
