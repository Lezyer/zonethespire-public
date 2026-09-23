using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Patches;

/// <summary>A clone of a doubled Shadow Corrupted value already holds the doubled number (never doubled twice).</summary>
[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.Clone))]
internal static class ShadowCorruptionVarClonePatch
{
    private static void Postfix(DynamicVar __instance, DynamicVar __result)
    {
        try
        {
            ShadowCorruption.CopyDoubled(__instance, __result);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow Corrupted value copy", ex);
        }
    }
}

/// <summary>Upgrading a doubled Shadow Corrupted value adds twice the upgrade, so the card stays exactly double.</summary>
[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.UpgradeValueBy))]
internal static class ShadowCorruptionVarUpgradePatch
{
    private static void Prefix(DynamicVar __instance, ref decimal addend)
    {
        try
        {
            if (ShadowCorruption.IsDoubled(__instance))
            {
                addend *= Core.Shadow.ShadowCorruptionRules.ValueMultiplier;
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow Corrupted upgrade", ex);
        }
    }
}

/// <summary>Before a card's values are previewed, a corrupted card's values rebuilt since (e.g. after loading) are doubled.</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview))]
internal static class ShadowCorruptionPreviewEnsurePatch
{
    private static void Prefix(CardModel __instance) => ShadowCorruption.EnsureDoubled(__instance);
}

/// <summary>
/// Block from a Shadow Corrupted card is doubled right after the card's enchantment is applied and before Dexterity and other
/// powers (BaseLib's card modifier Block hooks aren't wired up, unlike its damage hooks).
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class ShadowCorruptionBlockPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        List<CodeInstruction> code = instructions.ToList();
        ParameterInfo[] parameters = original.GetParameters();
        int blockArg = Array.FindIndex(parameters, p => p.Name == "block");
        int propsArg = Array.FindIndex(parameters, p => p.Name == "props");
        int cardArg = Array.FindIndex(parameters, p => p.Name == "cardSource");

        // num = block;
        int store = code.FindIndex(i => i.IsLdarg(blockArg));
        int firstHooks = code.FindIndex(i => i.opcode == OpCodes.Call && i.operand is MethodInfo { Name: "IterateCombatHookListeners" });
        if (blockArg < 0 || propsArg < 0 || cardArg < 0 || store < 0 || store + 1 >= code.Count || !code[store + 1].IsStloc() || firstHooks < 1
            || !code[firstHooks - 1].IsLdarg(0))
        {
            Log.Warn("Shadow Corruption: couldn't find where to double Block in Hook.ModifyBlock; corrupted cards won't double Block.");
            return code;
        }

        int local = code[store + 1].LocalIndex();
        // Insert before the arguments of the first hook loop: after the enchantment step.
        int insertAt = firstHooks - 1;
        var inserted = new List<CodeInstruction>
        {
            CodeInstruction.LoadLocal(local),
            CodeInstruction.LoadArgument(propsArg),
            CodeInstruction.LoadArgument(cardArg),
            CodeInstruction.Call(typeof(ShadowCorruption), nameof(ShadowCorruption.ModifyCardBlock)),
            CodeInstruction.StoreLocal(local),
        };
        inserted[0].MoveLabelsFrom(code[insertAt]);
        code.InsertRange(insertAt, inserted);
        return code;
    }
}

/// <summary>A Shadow Corrupted card's Block text shows the doubled Block (outside combat, and as its unmodified value).</summary>
[HarmonyPatch(typeof(BlockVar), nameof(BlockVar.UpdateCardPreview))]
internal static class ShadowCorruptionBlockPreviewPatch
{
    private static void Postfix(BlockVar __instance, CardModel card, bool runGlobalHooks)
    {
        try
        {
            if (!ShadowCorruptedModifier.IsCorrupted(card) || !__instance.Props.IsPoweredCardOrMonsterMoveBlock())
            {
                return;
            }

            // Recomputed from the base every time, so repeated previews never double twice.
            decimal value = __instance.BaseValue;
            if (card.Enchantment is { } enchantment)
            {
                value += enchantment.EnchantBlockAdditive(value);
                value *= enchantment.EnchantBlockMultiplicative(value);
            }

            value = ShadowCorruption.ModifyCardBlock(value, __instance.Props, card);
            if (!card.IsEnchantmentPreview)
            {
                __instance.EnchantedValue = value;
            }

            if (!runGlobalHooks)
            {
                __instance.PreviewValue = value;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to preview a Shadow Corrupted card's Block: {ex.Message}");
        }
    }
}

/// <summary>Same for calculated Block (the in-combat preview goes through Hook.ModifyBlock, which is already doubled).</summary>
[HarmonyPatch(typeof(CalculatedBlockVar), nameof(CalculatedBlockVar.UpdateCardPreview))]
internal static class ShadowCorruptionCalculatedBlockPreviewPatch
{
    private static void Postfix(CalculatedBlockVar __instance, CardModel card, bool runGlobalHooks)
    {
        try
        {
            if (!ShadowCorruptedModifier.IsCorrupted(card))
            {
                return;
            }

            // Only values this call just wrote from scratch are doubled, so repeated previews never double twice.
            decimal multiplier = Core.Shadow.ShadowCorruptionRules.ValueMultiplier;
            bool hasEnchantment = card.Enchantment != null;
            if (hasEnchantment && !card.IsEnchantmentPreview)
            {
                __instance.EnchantedValue *= multiplier;
            }

            if (!runGlobalHooks && (!card.IsEnchantmentPreview || hasEnchantment))
            {
                __instance.PreviewValue *= multiplier;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to preview a Shadow Corrupted card's calculated Block: {ex.Message}");
        }
    }
}

/// <summary>Swift on a Shadow Corrupted card draws twice its amount (the extra draw follows the enchantment's own).</summary>
[HarmonyPatch(typeof(Swift), nameof(Swift.OnPlay))]
internal static class ShadowCorruptionSwiftPatch
{
    private static void Prefix(Swift __instance, out bool __state) => __state = __instance.Status == EnchantmentStatus.Normal;

    private static void Postfix(Swift __instance, PlayerChoiceContext choiceContext, bool __state, ref Task __result)
    {
        try
        {
            if (__state && ShadowCorruptedModifier.IsCorrupted(__instance.Card))
            {
                __result = Extra(__result, () => CardPileCmd.Draw(choiceContext, __instance.Amount, __instance.Card.Owner));
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow Corrupted Swift", ex);
        }
    }

    /// <summary>The enchantment's own effect, then the extra one; only the extra one's failure is caught.</summary>
    internal static async Task Extra(Task original, Func<Task> extra)
    {
        await original;
        try
        {
            await extra();
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow Corrupted enchantment bonus", ex);
        }
    }
}

/// <summary>Sown on a Shadow Corrupted card gives twice its energy.</summary>
[HarmonyPatch(typeof(Sown), nameof(Sown.OnPlay))]
internal static class ShadowCorruptionSownPatch
{
    private static void Prefix(Sown __instance, out bool __state) => __state = __instance.Status == EnchantmentStatus.Normal;

    private static void Postfix(Sown __instance, bool __state, ref Task __result)
    {
        try
        {
            if (__state && ShadowCorruptedModifier.IsCorrupted(__instance.Card))
            {
                __result = ShadowCorruptionSwiftPatch.Extra(__result, () => PlayerCmd.GainEnergy(__instance.Amount, __instance.Card.Owner));
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shadow Corrupted Sown", ex);
        }
    }
}
