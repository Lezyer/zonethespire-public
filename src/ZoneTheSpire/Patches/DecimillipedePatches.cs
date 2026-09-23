using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ZoneTheSpire.Patches;

/// <summary>
/// When the last Decimillipede segment dies, ReattachPower fades out and frees the node of every enemy in combat, not just
/// the segments. Anything else still alive (Infestation Wrigglers, Scrapyard bots, other mods' spawns) kept fighting with no
/// visuals. The fade-out now only takes the segments (enemies with Reattach), which is identical in a vanilla fight.
/// </summary>
[HarmonyPatch(typeof(ReattachPower), "DoFadeOutOnAllSegments")]
internal static class ReattachFadeOutSegmentsOnlyPatch
{
    private static readonly MethodInfo EnemiesGetter = AccessTools.PropertyGetter(typeof(ICombatState), nameof(ICombatState.Enemies));
    private static readonly MethodInfo SegmentsGetter = AccessTools.Method(typeof(ReattachFadeOutSegmentsOnlyPatch), nameof(Segments));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(EnemiesGetter))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, SegmentsGetter).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (replaced == 0)
        {
            Log.Warn("Decimillipede fade-out: CombatState.Enemies not found; other enemies may still be faded out with the segments.");
        }
    }

    /// <summary>Replaces combatState.Enemies (same stack shape: takes the combat state, returns the list).</summary>
    private static IReadOnlyList<Creature> Segments(ICombatState combatState) =>
        combatState.Enemies.Where(enemy => enemy.HasPower<ReattachPower>()).ToList();
}
