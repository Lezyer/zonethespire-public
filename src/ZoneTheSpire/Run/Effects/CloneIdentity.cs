using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Which creatures are Mirrorlands clones. Clones are marked when created (on every peer, inside synced combat hooks), so
/// stripping Reflection doesn't let a clone summon.
/// </summary>
internal static class CloneIdentity
{
    private static readonly ConditionalWeakTable<Creature, object> Clones = new();
    private static readonly object Marker = new();

    public static void Mark(Creature creature) => Clones.AddOrUpdate(creature, Marker);

    public static bool IsClone(Creature? creature) =>
        creature != null && (Clones.TryGetValue(creature, out _) || creature.HasPower<ReflectionPower>());
}
