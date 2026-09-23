using System;
using System.Linq;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Run.Phantasmal;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Card damage previews against Phantasms. Phantasm lowers HP loss (ModifyHpLostAfterOsty), which card previews never run, so
/// hovering an attack over a Phantasm showed full damage. Like Intangible's preview, this shows the damage as if none of it is
/// blocked. Only preview calls are changed (real damage passes CardPreviewMode.None), so combat results are untouched. With
/// several targets (AoE cards) the reduced value shows only when every hittable enemy is a Phantasm, as vanilla does for
/// Vulnerable.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class PhantasmDamagePreviewPatch
{
    private static void Postfix(ICombatState? combatState, Creature? target, Creature? dealer, CardPreviewMode previewMode, ref decimal __result)
    {
        try
        {
            if (previewMode is not (CardPreviewMode.Normal or CardPreviewMode.MultiCreatureTargeting)
                || __result < 1m
                || dealer == null
                || dealer.Side == CombatSide.Enemy)
            {
                return;
            }

            bool reduced = target != null
                ? target.HasPower<PhantasmPower>()
                : previewMode == CardPreviewMode.MultiCreatureTargeting
                  && combatState?.HittableEnemies is { } enemies
                  && enemies.Any()
                  && enemies.All(enemy => enemy.HasPower<PhantasmPower>());
            if (reduced)
            {
                __result = PhantasmalRules.ReducedHpLoss((int)decimal.Floor(__result));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to preview Phantasm damage reduction: {ex}");
        }
    }
}
