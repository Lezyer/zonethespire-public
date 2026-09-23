using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ZoneTheSpire.Run;

/// <summary>
/// Temporarily removes our modifier from run modifier lists around vanilla code that reacts to "the run has
/// modifiers" (Neow, top bar, load screens). Always pair Hide with Restore in a Harmony finalizer.
/// </summary>
internal static class HiddenModifier
{
    private static readonly FieldInfo? ModifiersField = AccessTools.Field(typeof(RunState), "<Modifiers>k__BackingField");
    private static bool _warnedMissingField;

    public static IReadOnlyList<ModifierModel>? Hide(IRunState? runState)
    {
        try
        {
            if (runState is not RunState state || !state.Modifiers.Any(modifier => modifier is ZoneTheSpireModifier))
            {
                return null;
            }

            if (ModifiersField == null)
            {
                if (!_warnedMissingField)
                {
                    _warnedMissingField = true;
                    Log.Warn("RunState.Modifiers backing field not found; the hidden modifier may be visible to vanilla UI.");
                }

                return null;
            }

            IReadOnlyList<ModifierModel> original = state.Modifiers;
            ModifiersField.SetValue(state, original.Where(modifier => modifier is not ZoneTheSpireModifier).ToArray());
            return original;
        }
        catch (Exception ex)
        {
            Log.Warn($"HiddenModifier.Hide failed: {ex}");
            return null;
        }
    }

    public static void Restore(IRunState? runState, IReadOnlyList<ModifierModel>? original)
    {
        try
        {
            if (original != null && runState is RunState state)
            {
                ModifiersField?.SetValue(state, original);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"HiddenModifier.Restore failed: {ex}");
        }
    }

    public static bool IsOurs(SerializableModifier modifier) =>
        modifier.Id != null && modifier.Id.Equals(ModelDb.GetId<ZoneTheSpireModifier>());

    public static List<SerializableModifier>? HideSerialized(SerializableRun? run)
    {
        try
        {
            if (run?.Modifiers == null || !run.Modifiers.Any(IsOurs))
            {
                return null;
            }

            List<SerializableModifier> original = run.Modifiers;
            run.Modifiers = original.Where(modifier => !IsOurs(modifier)).ToList();
            return original;
        }
        catch (Exception ex)
        {
            Log.Warn($"HiddenModifier.HideSerialized failed: {ex}");
            return null;
        }
    }

    public static void RestoreSerialized(SerializableRun? run, List<SerializableModifier>? original)
    {
        try
        {
            if (run != null && original != null)
            {
                run.Modifiers = original;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"HiddenModifier.RestoreSerialized failed: {ex}");
        }
    }
}
