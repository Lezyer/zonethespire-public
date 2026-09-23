using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// What the local player can see in a Shadow Corruption fight, or in any fight while they hold the Last-Light Lantern. Enemy
/// intents are shown as the game's own Unknown intent unless the local player played Light the Way (or Lantern Light) this
/// turn. Purely local presentation: enemy moves and every other machine are unaffected, so each player only sees what they
/// revealed themselves.
/// </summary>
internal static class ShadowSight
{
    /// <summary>The game's "?" intent, shown in place of hidden intents (display only).</summary>
    public static readonly AbstractIntent Hidden = new UnknownIntent();

    private static WeakReference<ICombatState>? _revealedCombat;
    private static int _revealedTurn = -1;

    /// <summary>Whether <paramref name="enemy"/>'s intent should be hidden from the local player right now.</summary>
    public static bool ShouldHide(Creature enemy)
    {
        try
        {
            if (enemy.Side != CombatSide.Enemy || enemy.CombatState is not { } combat || IsRevealed(combat))
            {
                return false;
            }

            return ShadowRules.ShadowSightApplies(
                ZoneEffectQuery.IsActive(combat.RunState, ShadowCorruptionBiome.ShadowFightsEffectId),
                ZoneRelics.LastLightLantern.IsHeldLocally(combat));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check whether an intent is hidden: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Light the Way: reveals every enemy intent for the rest of this turn, on the machine of the player who played it only.
    /// </summary>
    public static void Reveal(Player player)
    {
        if (!LocalContext.IsMe(player) || player.Creature.CombatState is not { } combat)
        {
            return;
        }

        _revealedCombat = new WeakReference<ICombatState>(combat);
        _revealedTurn = player.PlayerCombatState?.TurnNumber ?? -1;
        RefreshEnemyIntents(combat);
    }

    private static bool IsRevealed(ICombatState combat)
    {
        if (_revealedCombat == null || !_revealedCombat.TryGetTarget(out ICombatState? revealed) || !ReferenceEquals(revealed, combat))
        {
            return false;
        }

        Player? me = LocalContext.GetMe(combat);
        return me?.PlayerCombatState?.TurnNumber == _revealedTurn;
    }

    /// <summary>Asks every living enemy's node to redraw its intents (they pass through the hide check again).</summary>
    private static void RefreshEnemyIntents(ICombatState combat)
    {
        if (NCombatRoom.Instance is not { } room)
        {
            return;
        }

        var targets = combat.Players.Select(player => player.Creature).ToList();
        foreach (Creature enemy in combat.Enemies.Where(enemy => enemy.IsAlive && enemy.Monster != null))
        {
            if (room.GetCreatureNode(enemy) is { } node)
            {
                TaskHelper.RunSafely(node.UpdateIntent(targets));
            }
        }

        RefreshLocalHealthBars(combat);
    }

    /// <summary>
    /// Refreshes the local player's health bar, so anything drawn from the intents next to it (e.g. another mod's incoming-damage
    /// number, which reads the intent damage hidden by ShadowIntentDamagePatch) updates as soon as intents are revealed or hidden
    /// again. Uses the game's own refresh; local presentation only.
    /// </summary>
    public static void RefreshLocalHealthBars(ICombatState combat)
    {
        try
        {
            if (NCombatRoom.Instance is not { } room || LocalContext.GetMe(combat) is not { } me || room.GetCreatureNode(me.Creature) is not { } node)
            {
                return;
            }

            var pending = new Stack<Node>();
            pending.Push(node);
            while (pending.Count > 0)
            {
                Node current = pending.Pop();
                if (current is NHealthBar bar)
                {
                    bar.RefreshValues();
                    continue;
                }

                foreach (Node child in current.GetChildren())
                {
                    pending.Push(child);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to refresh the health bar after revealing intents: {ex.Message}");
        }
    }
}
