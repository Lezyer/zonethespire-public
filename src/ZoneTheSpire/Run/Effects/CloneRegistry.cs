using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Creatures being added as mirrored clones right now, so creature-added hooks don't give them Mirrored.</summary>
internal static class CloneRegistry
{
    private static readonly HashSet<Creature> Pending = new();

    public static void Add(Creature creature) => Pending.Add(creature);

    public static void Remove(Creature creature) => Pending.Remove(creature);

    public static bool IsPending(Creature creature) => Pending.Contains(creature);

    /// <summary>Between runs (RunLifecycle): every add is paired with a remove in a finally, this only drops leftovers.</summary>
    internal static void Reset() => Pending.Clear();
}
