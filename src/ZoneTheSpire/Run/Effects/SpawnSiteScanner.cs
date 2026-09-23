using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Finds every monster and power method (game and other loaded mods) whose body calls CreatureCmd.Add, looking inside
/// async state machines. Covers summon moves and death triggers such as InfestedPower without listing them by hand.
/// </summary>
internal static class SpawnSiteScanner
{
    private const BindingFlags DeclaredInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static List<MethodBase> Find()
    {
        Assembly game = typeof(CreatureCmd).Assembly;
        string gameName = game.GetName().Name ?? string.Empty;
        Assembly self = typeof(SpawnSiteScanner).Assembly;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly != self && !assembly.IsDynamic)
            .Where(assembly => assembly == game || assembly.GetReferencedAssemblies().Any(reference => reference.Name == gameName))
            .SelectMany(LoadableTypes)
            .Where(type => typeof(MonsterModel).IsAssignableFrom(type) || typeof(PowerModel).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(DeclaredInstance))
            .Where(method => !method.IsAbstract && !method.ContainsGenericParameters && CallsCreatureAdd(method))
            .Cast<MethodBase>()
            .OrderBy(method => $"{method.DeclaringType?.FullName}.{method}", StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static bool CallsCreatureAdd(MethodInfo method)
    {
        try
        {
            MethodBase body = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? (MethodBase)method;
            return PatchProcessor.ReadMethodBody(body).Any(instruction =>
                instruction.Value is MethodInfo called
                && called.DeclaringType == typeof(CreatureCmd)
                && called.Name == nameof(CreatureCmd.Add));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
