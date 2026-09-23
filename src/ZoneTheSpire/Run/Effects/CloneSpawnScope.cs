using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// The clone whose code is running right now (its move, or one of its spawn sites). An AsyncLocal set in a Harmony prefix
/// flows into the patched async method and everything it awaits; the finalizer restores the caller's value.
/// </summary>
internal static class CloneSpawnScope
{
    private static readonly AsyncLocal<Creature?> CurrentClone = new();

    public readonly record struct Token(bool Entered, Creature? Previous);

    public static Creature? Current => CurrentClone.Value;

    public static Token EnterIfClone(Creature? actor)
    {
        if (!CloneIdentity.IsClone(actor))
        {
            return default;
        }

        Creature? previous = CurrentClone.Value;
        CurrentClone.Value = actor;
        return new Token(true, previous);
    }

    public static void Exit(Token token)
    {
        if (token.Entered)
        {
            CurrentClone.Value = token.Previous;
        }
    }
}
