using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Guardbot only shields Fabricators. Outside Fabricator fights (e.g. Scrapyard bots) it shields the first other living
/// enemy that isn't a Guardbot, else any other living enemy, else itself — re-chosen every turn so it keeps shielding
/// someone when its target dies. Combat order is identical on every peer. Fabricator fights keep vanilla behaviour.
/// </summary>
[HarmonyPatch(typeof(Guardbot), "GuardMove")]
internal static class GuardbotTargetPatch
{
    private const decimal GuardBlock = 15m;

    private static bool Prefix(Guardbot __instance, ref Task __result)
    {
        try
        {
            ICombatState? state = __instance.Creature.CombatState;
            if (state == null || state.Enemies.Any(enemy => enemy.Monster is Fabricator))
            {
                return true;
            }

            __result = Guard(__instance.Creature, state);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Guardbot targeting failed; using vanilla behaviour: {ex}");
            return true;
        }
    }

    private static async Task Guard(Creature self, ICombatState state)
    {
        await CreatureCmd.TriggerAnim(self, "Cast", 0.6f);
        Creature target = state.Enemies.FirstOrDefault(enemy => enemy != self && enemy.IsAlive && enemy.Monster is not Guardbot)
                          ?? state.Enemies.FirstOrDefault(enemy => enemy != self && enemy.IsAlive)
                          ?? self;
        await CreatureCmd.GainBlock(target, GuardBlock, ValueProp.Unpowered, null);
    }
}
