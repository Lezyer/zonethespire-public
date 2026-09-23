using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Run.Powers;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// The living-on side of the Worldly Attachment relic. The death check runs from ZoneTheSpireModifier's ShouldDieLate, which the
/// game reaches only after every power, relic and potion has had its say (run modifiers are the last hook listeners in both the
/// run-wide and combat lists), so Lizard Tail, Fairy in a Bottle and anything like them still save their owner first and this
/// only catches a death nothing else stopped. Once caught, the player keeps playing for 2 of their own turns
/// (<see cref="WorldlyAttachmentPower"/>); finishing the fight in that time heals them instead.
/// </summary>
internal static class WorldlyAttachmentLife
{
    /// <summary>Whether this death is held off: the holder's first death of the fight, and they aren't already living on.</summary>
    public static bool ShouldHold(Creature creature)
    {
        try
        {
            return creature.Player is { } player
                && CombatManager.Instance.IsInProgress
                && WorldlyAttachment.IsHeldBy(player)
                && !creature.HasPower<WorldlyAttachmentPower>()
                && !UsedThisCombat(player);
        }
        catch (Exception ex)
        {
            Log.Warn($"Worldly Attachment failed to check a death: {ex}");
            return false;
        }
    }

    /// <summary>The held-off death: the player lives on, untouchable, with their turns counting down.</summary>
    public static async Task Hold(Creature creature)
    {
        try
        {
            if (creature.Player is not { } player)
            {
                return;
            }

            MarkUsed(player);

            // The kill already took the player to 0 HP; they must be above it again or the game kills them straight away. Their
            // Max HP is never touched, so nothing follows them out of the fight.
            creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
            await CreatureCmd.SetCurrentHp(creature, creature.MaxHp);
            await PowerCmd.Apply<WorldlyAttachmentPower>(new ThrowingPlayerChoiceContext(), creature, PilgrimRules.AttachmentTurns, null, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Worldly Attachment failed to keep its holder alive: {ex}");
        }
    }

    /// <summary>
    /// The fight ended while its holder was living on: the attachment lets go. The infinite bar goes back to normal, the borrowed
    /// HP goes with it (down to 1), and then they heal 10% of their Max HP. This runs at the end of combat rather than on victory,
    /// because the game strips every power from the players before the victory hook.
    /// </summary>
    public static async Task OnCombatEnd(CombatRoom room)
    {
        foreach (Player player in room.CombatState.Players.ToList())
        {
            try
            {
                if (player.Creature is not { } creature || !creature.HasPower<WorldlyAttachmentPower>())
                {
                    continue;
                }

                creature.HpDisplay = HpDisplay.Normal;
                await PowerCmd.Remove<WorldlyAttachmentPower>(creature);
                await CreatureCmd.SetCurrentHp(creature, 1m);
                int healed = PilgrimRules.AttachmentHeal(creature.MaxHp);
                await CreatureCmd.Heal(creature, healed);
            }
            catch (Exception ex)
            {
                Log.Warn($"Worldly Attachment failed to see its holder out of the fight: {ex}");
            }
        }
    }

    /// <summary>Players whose attachment was already spent in the current combat (it holds one death per fight).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerCombatState, System.Collections.Generic.HashSet<ulong>> Used = new();

    private static bool UsedThisCombat(Player player) =>
        player.PlayerCombatState is { } combat && Used.TryGetValue(combat, out var used) && used.Contains(player.NetId);

    private static void MarkUsed(Player player)
    {
        if (player.PlayerCombatState is { } combat)
        {
            Used.GetValue(combat, static _ => new System.Collections.Generic.HashSet<ulong>()).Add(player.NetId);
        }
    }
}
