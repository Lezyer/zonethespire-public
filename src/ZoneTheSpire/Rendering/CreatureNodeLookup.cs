using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Finds a creature's node in the combat room, including while it is being removed. Some deaths take the node out of the
/// room's creature list before the death hooks run (Doom plays its death animation first and calls RemoveCreatureNode; so do
/// some other kill paths), but the node stays on screen in the room's "removing" list until its death animation ends.
/// </summary>
internal static class CreatureNodeLookup
{
    public static NCreature? Find(Creature? creature)
    {
        if (creature == null || NCombatRoom.Instance is not { } room)
        {
            return null;
        }

        return room.GetCreatureNode(creature) ?? room.RemovingCreatureNodes.FirstOrDefault(node => node.Entity == creature);
    }
}
