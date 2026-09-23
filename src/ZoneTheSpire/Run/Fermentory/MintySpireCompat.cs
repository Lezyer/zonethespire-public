using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using ZoneTheSpire.Core.Fermentory;

namespace ZoneTheSpire.Run.Fermentory;

/// <summary>
/// Optional Minty Spire support: its incoming damage number beside your health bar sums enemy attack intents (and a few
/// powers), which misses a Fire brew. When Minty Spire is loaded, its sum also counts every living enemy's Fire brew, through
/// the same damage hooks as the hit. Patched lazily the first time an enemy brews, so load order doesn't matter; without
/// Minty Spire this does nothing. Display only.
/// </summary>
internal static class MintySpireCompat
{
    private const string RenderType = "MintySpire2.MintySpire2Code.combat.SummedIncomingDamageRender";

    private static bool _tried;
    private static MethodInfo? _refresh;

    public static void EnsurePatched()
    {
        if (_tried)
        {
            return;
        }

        _tried = true;
        try
        {
            Type? render = AccessTools.TypeByName(RenderType);
            MethodInfo? calculate = render == null ? null : AccessTools.Method(render, "CalculateIncomingDamage", new[] { typeof(Creature) });
            if (calculate == null || calculate.ReturnType != typeof(int))
            {
                return;
            }

            new Harmony("reyzel.zonethespire.minty").Patch(calculate, postfix: new HarmonyMethod(typeof(MintySpireCompat), nameof(AddBrewDamage)));
            _refresh = AccessTools.Method(render, "CatchMonsterDeath");
            Log.Info("Minty Spire found: its incoming damage now counts Fermentory Fire brews.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Minty Spire support failed: {ex.Message}");
        }
    }

    /// <summary>Asks Minty Spire to redraw its number (after a brew appears or is drunk).</summary>
    public static void Refresh()
    {
        try
        {
            _refresh?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Minty Spire refresh failed: {ex.Message}");
        }
    }

    private static void AddBrewDamage(Creature creature, ref int __result)
    {
        try
        {
            if (creature.CombatState is not { } combat)
            {
                return;
            }

            foreach (Creature enemy in combat.Enemies.Where(enemy => enemy.IsAlive))
            {
                if (EnemyBrewing.BrewOf(enemy) is { Potion: EnemyPotion.Fire } brew)
                {
                    __result += EnemyBrewing.FireDamageTo(enemy, creature, brew);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Fermentory brews to Minty Spire's incoming damage: {ex.Message}");
        }
    }
}
