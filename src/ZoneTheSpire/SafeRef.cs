using System;
using HarmonyLib;

namespace ZoneTheSpire;

/// <summary>
/// Game fields read by reflection. <c>AccessTools.FieldRefAccess</c> throws when a game update renames a field, and in a static
/// initializer that surfaces inside the game's own code the first time the patch runs. This never throws while setting up:
/// a missing field logs one warning and gives a reference that throws when used, which the patch's own try/catch handles,
/// so only the feature that needs the field is skipped.
/// </summary>
internal static class SafeRef
{
    public static AccessTools.FieldRef<T, F> Field<T, F>(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<T, F>(name);
        }
        catch (Exception ex)
        {
            Log.Warn($"Game field {typeof(T).Name}.{name} not found ({ex.Message}); the feature that reads it is off.");
            return Missing<T, F>;
        }
    }

    private static ref F Missing<T, F>(T instance) => throw new MissingFieldException(typeof(T).Name, "(renamed game field)");
}
