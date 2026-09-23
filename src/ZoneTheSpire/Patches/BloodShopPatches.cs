using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Patches;

/// <summary>
/// In a Blood Rain shop every gold payment (item purchases, card removal, and the synced copies other peers apply) is paid
/// as HP instead. LoseGold is only redirected while the current room is a Blood Rain merchant room.
/// </summary>
[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseGold))]
internal static class BloodShopPaymentPatch
{
    private static bool Prefix(decimal amount, Player player, ref Task __result)
    {
        try
        {
            if (!BloodShop.IsActive(player) && !BloodShop.LedgerCovers(player, (int)amount))
            {
                return true;
            }

            __result = BloodShop.PayWithHp(player, amount);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Blood Rain shop payment failed to start; paying gold: {ex}");
            return true;
        }
    }
}

/// <summary>
/// MerchantEntry.EnoughGold is a one-line getter the JIT can inline into its callers, so instead of patching it, every shop
/// method that reads it (purchase checks, including async state machines, and price colouring) calls
/// BloodShop.EnoughCurrency, which checks HP in Blood Rain shops and falls back to EnoughGold everywhere else.
/// </summary>
[HarmonyPatch]
internal static class BloodShopAffordabilityPatch
{
    private static readonly MethodInfo EnoughGoldGetter = AccessTools.PropertyGetter(typeof(MerchantEntry), nameof(MerchantEntry.EnoughGold));
    private static readonly MethodInfo EnoughCurrency = AccessTools.Method(typeof(BloodShop), nameof(BloodShop.EnoughCurrency));

    private static readonly string[] ShopNamespaces =
    {
        "MegaCrit.Sts2.Core.Entities.Merchant",
        "MegaCrit.Sts2.Core.Nodes.Screens.Shops",
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new List<MethodBase>();
        foreach (Type type in typeof(MerchantEntry).Assembly.GetTypes())
        {
            string? ns = type.Namespace ?? type.DeclaringType?.Namespace;
            if (ns == null || !ShopNamespaces.Contains(ns) || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.IsAbstract || method.IsGenericMethodDefinition || method.GetMethodBody() == null)
                {
                    continue;
                }

                try
                {
                    if (PatchProcessor.ReadMethodBody(method).Any(instruction => instruction.Value is MethodInfo called && called == EnoughGoldGetter))
                    {
                        targets.Add(method);
                    }
                }
                catch (Exception)
                {
                    // Unreadable body: not a call site we can patch.
                }
            }
        }

        if (targets.Count == 0)
        {
            Log.Warn("Blood Rain shop affordability: no EnoughGold call sites found; HP prices won't be checked against HP.");
        }
        return targets;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(EnoughGoldGetter))
            {
                yield return new CodeInstruction(OpCodes.Call, EnoughCurrency).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }
    }
}
