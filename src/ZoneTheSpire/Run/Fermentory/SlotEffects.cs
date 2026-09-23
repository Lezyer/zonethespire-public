using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Fermentory;

namespace ZoneTheSpire.Run.Fermentory;

/// <summary>
/// What special potion slots do when a potion is used from them. The game empties the slot before the potion takes effect
/// (<c>PotionModel.RemoveBeforeUse</c>), so the slot is remembered there and its effect runs after the use. Potion use runs on
/// every peer, and every roll uses the run's synced RNG, so all peers agree.
/// </summary>
internal static class SlotEffects
{
    private static readonly ConditionalWeakTable<PotionModel, StrongBox<int>> UsedFromSlot = new();

    /// <summary>Extra uses made by Duplicating Solution and Distribution System: they never trigger slot effects themselves.</summary>
    private static readonly ConditionalWeakTable<PotionModel, object> ExtraUses = new();

    private static readonly MethodInfo? OnUse = AccessTools.Method(typeof(PotionModel), "OnUse");

    public static bool IsExtraUse(PotionModel potion) => ExtraUses.TryGetValue(potion, out _);

    public static void RememberSlot(PotionModel potion)
    {
        if (IsExtraUse(potion))
        {
            return;
        }

        int slot = potion.Owner.GetPotionSlotIndex(potion);
        UsedFromSlot.AddOrUpdate(potion, new StrongBox<int>(slot));
    }

    public static async Task AfterUse(Task use, PotionModel potion, PlayerChoiceContext choiceContext, Creature? target)
    {
        await use;
        try
        {
            if (IsExtraUse(potion) || !UsedFromSlot.TryGetValue(potion, out StrongBox<int>? box))
            {
                return;
            }

            UsedFromSlot.Remove(potion);
            Player owner = potion.Owner;
            if (owner.Creature.IsDead || SpecialSlots.EffectAt(owner, box.Value) is not SlotEffect effect)
            {
                return;
            }

            await Apply(effect, potion, owner, box.Value, choiceContext, target);
        }
        catch (Exception ex)
        {
            Log.Warn($"A special potion slot failed after {potion.Id.Entry}: {ex}");
        }
    }

    private static async Task Apply(SlotEffect effect, PotionModel potion, Player owner, int slot, PlayerChoiceContext choiceContext, Creature? target)
    {
        IRunState runState = owner.RunState;
        ICombatState? combat = owner.Creature.CombatState;
        bool inCombat = combat != null && CombatManager.Instance.IsInProgress;
        switch (effect)
        {
            case SlotEffect.HealingBalm:
                await CreatureCmd.Heal(owner.Creature, FermentoryRules.HealingBalmHeal);
                break;
            case SlotEffect.EmpoweringDraught when inCombat:
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), owner.Creature, FermentoryRules.EmpoweringStrength, owner.Creature, null);
                break;
            case SlotEffect.WardingFlask when inCombat:
                await CreatureCmd.GainBlock(owner.Creature, FermentoryRules.WardingBlock, ValueProp.Unpowered, null);
                break;
            case SlotEffect.QuickeningTonic when inCombat:
                for (int i = 0; i < FermentoryRules.QuickeningDraw; i++)
                {
                    await CardPileCmd.Draw(choiceContext, owner);
                }

                break;
            case SlotEffect.VolatileMixture when inCombat:
                List<Creature> enemies = combat!.GetCreaturesOnSide(CombatSide.Enemy).Where(c => c.IsHittable).ToList();
                if (enemies.Count > 0)
                {
                    await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), enemies, FermentoryRules.VolatileDamage, ValueProp.Unpowered, owner.Creature);
                }

                break;
            case SlotEffect.BottomlessMechanism:
                if (runState.Rng.Niche.NextInt(100) < FermentoryRules.KeepChancePercent)
                {
                    await Refill(owner, slot, ModelDb.GetById<PotionModel>(potion.Id).ToMutable());
                }

                break;
            case SlotEffect.EntropicSolvent:
                if (runState.Rng.Niche.NextInt(100) < FermentoryRules.EntropicChancePercent)
                {
                    PotionModel random = inCombat
                        ? PotionFactory.CreateRandomPotionInCombat(owner, runState.Rng.CombatPotionGeneration)
                        : PotionFactory.CreateRandomPotionOutOfCombat(owner, runState.Rng.Niche);
                    await Refill(owner, slot, random.ToMutable());
                }

                break;
            case SlotEffect.DuplicatingSolution:
                if (ResolveTarget(potion, owner, target, target, runState.Rng.CombatTargets, combat) is var (ok, again) && ok)
                {
                    await ExtraUse(potion, owner, again, choiceContext);
                }

                break;
            case SlotEffect.DistributionSystem when runState.Players.Count > 1:
                foreach (Player other in runState.Players.Where(p => p != owner && !p.Creature.IsDead).ToList())
                {
                    Creature? mapped = target != null && target.IsPlayer ? other.Creature : target;
                    if (ResolveTarget(potion, other, mapped, target, runState.Rng.CombatTargets, combat) is var (ok2, forOther) && ok2)
                    {
                        await ExtraUse(potion, other, forOther, choiceContext);
                    }
                }

                break;
        }
    }

    /// <summary>At the start of each combat, every empty Refilling Still slot gets a random potion.</summary>
    public static async Task FillStills(IRunState runState)
    {
        foreach (Player player in runState.Players)
        {
            try
            {
                foreach ((int slot, SlotEffect effect) in SpecialSlots.Get(player).OrderBy(pair => pair.Key))
                {
                    if (effect == SlotEffect.RefillingStill && slot < player.MaxPotionCount && player.PotionSlots[slot] == null)
                    {
                        PotionModel potion = PotionFactory.CreateRandomPotionInCombat(player, runState.Rng.CombatPotionGeneration).ToMutable();
                        await Refill(player, slot, potion);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Refilling Still failed for player {player.NetId}: {ex}");
            }
        }
    }

    private static async Task Refill(Player owner, int slot, PotionModel potion)
    {
        if (slot < owner.MaxPotionCount && owner.PotionSlots[slot] == null)
        {
            await PotionCmd.TryToProcure(potion, owner, slot);
        }
    }

    /// <summary>
    /// Where an extra use goes: no target stays no target; a living, valid target stays; an enemy that died is swapped for a
    /// random living enemy; anything else (a dead player) skips the use.
    /// </summary>
    private static (bool Ok, Creature? Target)? ResolveTarget(PotionModel potion, Player user, Creature? target, Creature? original, Rng rng, ICombatState? combat)
    {
        if (target == null)
        {
            return (original == null, null);
        }

        if (target.IsAlive)
        {
            return (true, target);
        }

        if (combat != null && target.Side == CombatSide.Enemy)
        {
            List<Creature> living = combat.GetCreaturesOnSide(CombatSide.Enemy).Where(c => c.IsHittable).ToList();
            return living.Count > 0 ? (true, rng.NextItem(living)) : (false, null);
        }

        return (false, null);
    }

    /// <summary>
    /// Uses a fresh copy of <paramref name="potion"/> owned by <paramref name="user"/> on <paramref name="target"/>, the way
    /// PotionModel.OnUseWrapper runs its effect, without touching any slot or firing the potion-used hooks again.
    /// </summary>
    private static async Task ExtraUse(PotionModel potion, Player user, Creature? target, PlayerChoiceContext choiceContext)
    {
        if (OnUse == null)
        {
            Log.Warn("PotionModel.OnUse not found; extra potion use skipped.");
            return;
        }

        PotionModel copy = ModelDb.GetById<PotionModel>(potion.Id).ToMutable();
        copy.Owner = user;
        ExtraUses.AddOrUpdate(copy, new object());
        CombatId? effectCombatId = CombatManager.Instance.BeginCardOrPotionEffect(user);
        try
        {
            var branching = new BranchingPlayerChoiceContext(copy, LocalContext.NetId!.Value, GameActionType.Combat, choiceContext);
            branching.PushModel(copy);
            var task = (Task)OnUse.Invoke(copy, new object?[] { branching, target })!;
            await branching.AssignTaskAndWaitForPauseOrCompletion(task);
        }
        finally
        {
            await CombatManager.Instance.EndCardOrPotionEffect(effectCombatId, user);
        }
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.RemoveBeforeUse))]
internal static class SpecialSlotCapturePatch
{
    private static void Prefix(PotionModel __instance)
    {
        try
        {
            SlotEffects.RememberSlot(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to remember a potion's slot: {ex}");
        }
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.OnUseWrapper))]
internal static class SpecialSlotUsePatch
{
    private static void Postfix(PotionModel __instance, PlayerChoiceContext choiceContext, Creature? target, ref Task __result)
    {
        __result = SlotEffects.AfterUse(__result, __instance, choiceContext, target);
    }
}
