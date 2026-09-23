using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneTheSpire.Core.Mirror;

/// <summary>Pure choice of what a Mirrorlands clone does instead of a summoning move. No RNG: every peer picks the same move.</summary>
public static class SummonGuard
{
    /// <summary>
    /// Null when <paramref name="next"/> doesn't summon (or nothing non-summoning exists). Otherwise the last performed move,
    /// else the first non-summoning follow-up of <paramref name="next"/>, else the first non-summoning move overall.
    /// The alternatives are only enumerated when <paramref name="next"/> summons.
    /// </summary>
    public static T? ChooseReplacement<T>(T next, T? lastPerformed, IEnumerable<T> followUps, IEnumerable<T> allMoves, Func<T, bool> isSummon)
        where T : class
    {
        if (!isSummon(next))
        {
            return null;
        }

        if (lastPerformed != null && !isSummon(lastPerformed))
        {
            return lastPerformed;
        }

        return followUps.FirstOrDefault(move => !isSummon(move)) ?? allMoves.FirstOrDefault(move => !isSummon(move));
    }
}
