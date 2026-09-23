using System;
using System.Collections.Generic;
using Godot;

namespace ZoneTheSpire;

internal static class Log
{
    private const string Prefix = "[ZoneTheSpire] ";

    internal static void Info(string message) => GD.Print(Prefix + message);

    internal static void Warn(string message) => GD.PushWarning(Prefix + message);

    internal static void Error(string message) => GD.PushError(Prefix + message);

    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    /// <summary>
    /// A patch failed and fell back to the game's own behaviour: logged the first time for each place, not on every call (a
    /// patch on a hot path would otherwise flood the log).
    /// </summary>
    internal static void WarnOnce(string where, Exception ex)
    {
        lock (Reported)
        {
            if (!Reported.Add(where))
            {
                return;
            }
        }

        Warn($"{where} failed; the game's own behaviour is kept (repeats are not logged): {ex}");
    }
}
