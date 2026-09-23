using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Where each creature's node last stood before it left the combat room. Spawning something in a dead enemy's place can't
/// always read the node itself: a Doom death removes the node and queues it free before the kill runs, so by the time the
/// death hooks fire the node may already be gone (RemovingCreatureNodes only keeps nodes that are still valid). The room's
/// removal is patched (CreatureDeathSpotPatch) to record the position at the moment the node leaves, which is the last point
/// where it is certainly still there. Local rendering only; nothing here affects gameplay.
/// </summary>
internal static class CreatureDeathSpots
{
    private static readonly ConditionalWeakTable<Creature, StrongBox<Vector2>> Positions = new();

    /// <summary>Remembers where this node stood. Called as the room lets the node go.</summary>
    public static void Record(NCreature node)
    {
        try
        {
            if (GodotObject.IsInstanceValid(node) && node.Entity is { } creature)
            {
                Positions.AddOrUpdate(creature, new StrongBox<Vector2>(node.Position));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to record where a creature's node stood: {ex.Message}");
        }
    }

    /// <summary>Where the creature's node stood when it left the room, or null when it never did.</summary>
    public static Vector2? LastPosition(Creature? creature) =>
        creature != null && Positions.TryGetValue(creature, out StrongBox<Vector2>? position) ? position.Value : null;
}
