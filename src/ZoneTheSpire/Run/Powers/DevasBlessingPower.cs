using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Deva's Domain (every enemy that doesn't already come back from death on its own; see <see cref="DevasBlessing"/>): the first
/// time the enemy would die, it doesn't. Its health bar becomes the purple infinite bar (the Waterfall Giant's) over
/// <see cref="BlessedHp"/> HP, and it dies right after its next action (DevasBlessingTakeTurnPatch). Hits still land in full
/// (damage, block, poison and every on-hit or damage-dealt effect, which some enemies' mechanics rely on); they only come out
/// of a pool it can't run out of before that action. Killed by Doom (end of the enemy
/// turn), it lives through the players' next turn and dies after acting in the following enemy turn. Decimillipede segments
/// revive each other as usual; only when the last one would die do they all come back blessed, act once each, then die.
/// The death is prevented rather than undone, so on-death effects only trigger on the real death afterwards; the killing hit
/// still counts as a kill. Everything runs in synced combat hooks. Its icon is served by BloodDrinkerIconPatch.
/// </summary>
public sealed class DevasBlessingPower : CustomPowerModel
{
    /// <summary>The value both max and current HP are set to while blessed (shown as the infinite bar).</summary>
    private const decimal BlessedHp = 999999999m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<RadiancePower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<RadiancePower>().ResolvedBigIconPath;

    /// <summary>Whether the blessing was used: the enemy is living on and dies after its next action.</summary>
    public bool IsBlessed { get; private set; }

    /// <summary>Kept on a dead Decimillipede segment, so the whole group can come back blessed.</summary>
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override bool ShouldDie(Creature creature)
    {
        try
        {
            return creature != Owner || IsBlessed || !DevasBlessing.ShouldPrevent(Owner);
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to check a death: {ex}");
            return true;
        }
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        try
        {
            if (creature != Owner)
            {
                return;
            }

            // A Decimillipede's other segments are lying dead (reviving): all of them get HP first, then they reattach.
            IReadOnlyList<Creature> group = DevasBlessing.GroupOf(Owner);
            List<Creature> reviving = group.Where(member => member != Owner && member.IsDead).ToList();
            foreach (Creature member in group)
            {
                if (member.GetPower<DevasBlessingPower>() is { IsBlessed: false } power)
                {
                    await power.LiveOn();
                }
            }

            for (int i = 0; i < reviving.Count; i++)
            {
                await DevasBlessing.ReviveSegment(reviving[i], i);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Deva's Blessing failed to keep an enemy alive: {ex}");
        }
    }

    /// <summary>Turns the creature into its blessed state: the infinite purple bar over an HP pool no fight can empty.</summary>
    private async Task LiveOn()
    {
        IsBlessed = true;
        // Set before the HP change, so the health bar redraws as the infinite bar straight away.
        Owner.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
        await MegaCrit.Sts2.Core.Commands.CreatureCmd.SetMaxAndCurrentHp(Owner, BlessedHp);
        Flash();
    }
}
