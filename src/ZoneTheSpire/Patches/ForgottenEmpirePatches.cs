using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.ForgottenEmpire;
using ZoneTheSpire.Run.Powers;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Marbled cards: a Marbled card gives a player all of its Block as Marbled, plus half of it (rounded down) as Block. The
/// replacement runs the same block hooks as CreatureCmd.GainBlock (so Dexterity, Frail and relics still scale it), adds the
/// Marbled, then gains the halved Block the way the game does and returns it. Card plays are synced, so every peer gets the
/// same amounts.
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool) })]
internal static class MarbledBlockGainPatch
{
    private static bool Prefix(Creature creature, decimal amount, ValueProp props, CardPlay? cardPlay, bool fast, ref Task<decimal> __result)
    {
        try
        {
            if (!creature.IsPlayer || !MarbleCards.IsMarbled(cardPlay?.Card))
            {
                return true;
            }

            __result = GainMarbled(creature, amount, props, cardPlay!, fast);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to turn Marbled card Block into Marbled: {ex}");
            return true;
        }
    }

    private static async Task<decimal> GainMarbled(Creature creature, decimal amount, ValueProp props, CardPlay cardPlay, bool fast)
    {
        if (CombatManager.Instance.IsOverOrEnding || creature.IsDead)
        {
            return 0m;
        }

        ICombatState combatState = creature.CombatState!;
        await Hook.BeforeBlockGained(combatState, creature, amount, props, cardPlay.Card);
        decimal modified = Hook.ModifyBlock(combatState, creature, amount, props, cardPlay.Card, cardPlay, out IEnumerable<AbstractModel> modifiers);
        modified = Math.Max(modified, 0m);
        await Hook.AfterModifyingBlockAmount(combatState, modified, cardPlay.Card, cardPlay, modifiers);
        int marbled = (int)modified;
        if (marbled > 0)
        {
            await MarbledPower.Add(creature, marbled, cardPlay.Card);
        }

        int block = ForgottenEmpireRules.MarbledCardBlock(marbled);
        if (block > 0)
        {
            SfxCmd.Play("event:/sfx/block_gain");
            VfxCmd.PlayOnCreatureCenter(creature, "vfx/vfx_block");
            creature.GainBlockInternal(block);
            CombatManager.Instance.History.BlockGained(combatState, creature, block, props, cardPlay);
        }

        if (marbled > 0)
        {
            await Cmd.CustomScaledWait(fast ? 0f : 0.1f, fast ? 0.03f : 0.25f);
        }

        await Hook.AfterBlockGained(combatState, creature, block, props, cardPlay.Card);
        return block;
    }
}

/// <summary>
/// HP-loss layers. Marbled absorbs ordinary damage right after Block, on the creature that was hit, before the game redirects
/// the rest to Osty or a Wriggler guard (the "before Osty" phase, after every model modifier of that phase). Then, as the final
/// layer after the redirection, Golden Wishmaker evaluates only the HP loss that remains, so Marbled can never make its gold
/// cost free. Wishmaker itself
/// is armed by BeforeDamageReceived, so damage previews that call Hook.ModifyHpLost cannot trigger it; Marbled, which spends
/// itself when it absorbs, skips a call that asks for both phases at once (previews) for the same reason. Each layer adds itself
/// to the hook's modifiers when it changes the HP loss, so the game and other mods can tell what took the damage. Unblockable HP
/// loss bypasses Marbled, like Block, but can still be bought off by Wishmaker.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class MarbledHpLossPatch
{
    private static void Postfix(
        Creature target,
        ValueProp props,
        HpLossHookPhase phases,
        ref decimal __result,
        ref IEnumerable<AbstractModel> modifiers)
    {
        try
        {
            if (__result < 1m)
            {
                return;
            }

            // Marbled sits on the creature that was hit, before Osty or a Wriggler guard take the rest. Absorbing spends the
            // layer, so it only runs for that phase on its own: the game asks for both phases at once (HpLossHookPhase.All) only
            // where there is no redirection step, which its own docs give damage previews as the example of.
            if (phases == HpLossHookPhase.BeforeOsty && !props.HasFlag(ValueProp.Unblockable))
            {
                MarbledPower? marbled = target.GetPower<MarbledPower>();
                if (marbled != null && marbled.Amount > 0)
                {
                    decimal beforeMarble = __result;
                    __result = marbled.Absorb(__result);
                    if (__result != beforeMarble)
                    {
                        // The game's own way of saying which model changed the value: recaps and other mods can name Marbled.
                        modifiers = modifiers.Append(marbled).ToList();
                    }
                }
            }

            if (!phases.HasFlag(HpLossHookPhase.AfterOsty))
            {
                return;
            }

            if (__result >= 1m && target.Player?.GetRelic<GoldenWishmaker>() is { } wishmaker)
            {
                decimal beforeWish = __result;
                __result = wishmaker.ModifyFinalHpLoss(target, __result);
                if (decimal.Truncate(beforeWish) != decimal.Truncate(__result))
                {
                    modifiers = modifiers.Append(wishmaker).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Marbled failed to absorb damage: {ex}");
        }
    }
}

/// <summary>
/// Marbled cards show the Block they really give (half) and their full Block as "Gain X Marbled.": after the game refreshes a
/// card's displayed values, the Block display values are halved. Skips canonical cards (the game doesn't preview them either)
/// and previews of another card's values.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview))]
internal static class MarbledCardPreviewPatch
{
    private static bool _warned;

    private static void Postfix(CardModel __instance, DynamicVarSet dynamicVarSet)
    {
        try
        {
            if ((__instance.RunState == null && __instance.CombatState == null) || dynamicVarSet != __instance.DynamicVars)
            {
                return;
            }

            MarbledModifier.AfterPreviewUpdated(__instance);
        }
        catch (Exception ex)
        {
            if (!_warned)
            {
                _warned = true;
                Log.Warn($"Failed to update a Marbled card's Block text: {ex}");
            }
        }
    }
}

/// <summary>Remembers which health bars show which creature, so Marbled changes can refresh them.</summary>
[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class MarbleHealthBarRegisterPatch
{
    private static void Postfix(NHealthBar __instance, Creature creature) => MarbleHealthBar.Register(creature, __instance);
}

/// <summary>After the game refreshes a health bar: show the Marbled shield, and whiten enemy bars that have Marbled.</summary>
[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class MarbleHealthBarRefreshPatch
{
    private static void Postfix(NHealthBar __instance) => MarbleHealthBar.OnRefreshed(__instance);
}
