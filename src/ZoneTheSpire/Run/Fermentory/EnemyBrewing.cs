using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Fermentory;

/// <summary>An enemy's potion for this round: what it is and its number.</summary>
internal sealed record Brew(EnemyPotion Potion, int Amount, int Round);

/// <summary>
/// Fermentory enemies take turns brewing a potion (FermentoryRules.Brews by combat id and round). The brew is picked at the start
/// of the players' turn from the run seed, the location, the enemy and the round, so every peer brews the same, and it is drunk
/// right after that enemy's turn (Creature.TakeTurn), unless the enemy died first.
/// </summary>
internal static class EnemyBrewing
{
    private static readonly ConditionalWeakTable<Creature, Brew> Brews = new();

    public static Brew? BrewOf(Creature creature) => Brews.TryGetValue(creature, out Brew? brew) ? brew : null;

    /// <summary>Called for each player after their turn starts; brewing happens once per round.</summary>
    public static void BrewForRound(ICombatState combat, IRunState runState)
    {
        MintySpireCompat.EnsurePatched();
        int round = combat.RoundNumber;
        string location = SpecialSlots.CurrentLocation(runState);
        foreach (Creature enemy in combat.GetCreaturesOnSide(CombatSide.Enemy).Where(c => c.IsAlive && c.IsMonster).ToList())
        {
            if (enemy.CombatId is not uint id || (BrewOf(enemy) is { } existing && existing.Round == round))
            {
                continue;
            }

            if (!FermentoryRules.Brews((int)(id % 1024), round))
            {
                Brews.Remove(enemy);
                EnemyBrewVisuals.Clear(enemy);
                continue;
            }

            EnemyPotion potion = FermentoryRules.PickEnemyPotion(runState.Rng.Seed, location, id.ToString(CultureInfo.InvariantCulture), round);
            var brew = new Brew(potion, FermentoryRules.Amount(potion, runState.CurrentActIndex, runState.Players.Count), round);
            Brews.AddOrUpdate(enemy, brew);
            EnemyBrewVisuals.Show(enemy, brew, runState.Players.Count > 1);
        }

        MintySpireCompat.Refresh();
    }

    /// <summary>After an enemy's turn: it drinks its brew (if it still lives).</summary>
    public static async Task AfterTurn(Task turn, Creature enemy)
    {
        await turn;
        try
        {
            if (BrewOf(enemy) is not { } brew)
            {
                return;
            }

            Brews.Remove(enemy);
            EnemyBrewVisuals.Clear(enemy);
            MintySpireCompat.Refresh();
            if (enemy.IsDead || enemy.CombatState is not { } combat)
            {
                return;
            }

            await Drink(enemy, combat, brew);
        }
        catch (Exception ex)
        {
            Log.Warn($"An enemy brew failed: {ex}");
        }
    }

    private static async Task Drink(Creature enemy, ICombatState combat, Brew brew)
    {
        var context = new ThrowingPlayerChoiceContext();
        List<Creature> players = combat.GetCreaturesOnSide(CombatSide.Player).Where(c => c.IsPlayer && c.IsAlive).ToList();
        switch (brew.Potion)
        {
            case EnemyPotion.Strength:
                await PowerCmd.Apply<StrengthPower>(context, enemy, brew.Amount, enemy, null);
                break;
            case EnemyPotion.Block:
                await CreatureCmd.GainBlock(enemy, brew.Amount, ValueProp.Unpowered, null);
                break;
            case EnemyPotion.Regen:
                await PowerCmd.Apply<RegenPower>(context, enemy, brew.Amount, enemy, null);
                break;
            case EnemyPotion.LiquidBronze:
                await PowerCmd.Apply<ThornsPower>(context, enemy, brew.Amount, enemy, null);
                break;
            case EnemyPotion.Weak when players.Count > 0:
                await PowerCmd.Apply<WeakPower>(context, players, brew.Amount, enemy, null);
                break;
            case EnemyPotion.Vulnerable when players.Count > 0:
                await PowerCmd.Apply<VulnerablePower>(context, players, brew.Amount, enemy, null);
                break;
            case EnemyPotion.Poison when players.Count > 0:
                await PowerCmd.Apply<PoisonPower>(context, players, brew.Amount, enemy, null);
                break;
            case EnemyPotion.Fire when players.Count > 0:
                await CreatureCmd.Damage(context, players, brew.Amount, ValueProp.Unpowered, enemy);
                break;
            case EnemyPotion.Blood:
                await CreatureCmd.Heal(enemy, FermentoryRules.BloodHeal(enemy.MaxHp));
                break;
        }
    }

    /// <summary>
    /// The damage a Fire brew will deal <paramref name="target"/>, through the same damage hooks as the real hit (Vulnerable,
    /// Intangible, relics), like a vanilla attack intent's number.
    /// </summary>
    public static int FireDamageTo(Creature enemy, Creature target, Brew brew)
    {
        if (enemy.CombatState is not { } combat)
        {
            return brew.Amount;
        }

        decimal damage = Hook.ModifyDamage(combat.RunState, combat, target, enemy, brew.Amount, ValueProp.Unpowered, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
        return Math.Max(0, (int)damage);
    }

    /// <summary>The vanilla potion an enemy potion looks like.</summary>
    public static PotionModel VanillaPotion(EnemyPotion potion) => potion switch
    {
        EnemyPotion.Strength => ModelDb.Potion<StrengthPotion>(),
        EnemyPotion.Block => ModelDb.Potion<BlockPotion>(),
        EnemyPotion.Regen => ModelDb.Potion<RegenPotion>(),
        EnemyPotion.LiquidBronze => ModelDb.Potion<LiquidBronze>(),
        EnemyPotion.Weak => ModelDb.Potion<WeakPotion>(),
        EnemyPotion.Vulnerable => ModelDb.Potion<VulnerablePotion>(),
        EnemyPotion.Poison => ModelDb.Potion<PoisonPotion>(),
        EnemyPotion.Fire => ModelDb.Potion<FirePotion>(),
        _ => ModelDb.Potion<BloodPotion>(),
    };
}

[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
internal static class EnemyBrewDrinkPatch
{
    private static void Postfix(Creature __instance, ref Task __result)
    {
        __result = EnemyBrewing.AfterTurn(__result, __instance);
    }
}
