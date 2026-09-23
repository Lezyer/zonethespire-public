using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Blood Rain (Monster, Elite, "?" fights): every enemy has Blood Drinker (with a crimson mist), including enemies added
/// during the fight; winning gives every living player +3 Max HP (+5 in Elite rooms). All inside synced combat hooks.
/// </summary>
internal sealed class BloodRainHandler : ZoneEffectHandler
{
    public override string EffectId => BloodRainBiome.BloodDrinkersEffectId;

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await GiveBloodDrinker(enemy);
        }
    }

    /// <summary>Enemies summoned or spawned mid-fight. The fight's starting enemies get it in OnBeforeCombatStart.</summary>
    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await GiveBloodDrinker(creature);
        }
    }

    public override async Task OnCombatVictory(CombatRoom room, ZoneContext context)
    {
        int amount = BloodRainRules.MaxHpReward(room.RoomType == RoomType.Elite);
        foreach (Player player in room.CombatState.Players)
        {
            // Max HP gain also heals by the same amount; players who died in the fight are left to the game's revive.
            if (player.Creature.IsDead)
            {
                continue;
            }

            await CreatureCmd.GainMaxHp(player.Creature, amount);
        }
    }

    private static async Task GiveBloodDrinker(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || creature.HasPower<BloodDrinkerPower>())
        {
            return;
        }

        await PowerCmd.Apply<BloodDrinkerPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
    }
}
