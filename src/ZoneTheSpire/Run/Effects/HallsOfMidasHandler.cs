using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Halls of Midas (Monster, Elite, "?" fights): every enemy has Touch of Midas (with a golden sheen), including enemies added
/// during the fight. All inside synced combat hooks.
/// </summary>
internal sealed class HallsOfMidasHandler : ZoneEffectHandler
{
    public override string EffectId => HallsOfMidasBiome.TouchOfMidasEffectId;

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await GiveTouchOfMidas(enemy);
        }
    }

    /// <summary>Enemies summoned or spawned mid-fight. The fight's starting enemies get it in OnBeforeCombatStart.</summary>
    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await GiveTouchOfMidas(creature);
        }
    }

    private static async Task GiveTouchOfMidas(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || creature.HasPower<TouchOfMidasPower>())
        {
            return;
        }

        await PowerCmd.Apply<TouchOfMidasPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
    }
}
