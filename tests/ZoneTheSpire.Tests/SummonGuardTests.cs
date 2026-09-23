using System;
using ZoneTheSpire.Core.Mirror;
using Xunit;

namespace ZoneTheSpire.Tests;

public class SummonGuardTests
{
    private sealed record Move(string Id, bool Summons);

    private static readonly Move Scratch = new("SCRATCH", false);
    private static readonly Move Bite = new("BITE", false);
    private static readonly Move CallForBackup = new("CALL_FOR_BACKUP", true);
    private static readonly Move Bloat = new("BLOAT", true);

    private static Move? Choose(Move next, Move? lastPerformed, Move[] followUps, Move[] allMoves) =>
        SummonGuard.ChooseReplacement(next, lastPerformed, followUps, allMoves, move => move.Summons);

    [Fact]
    public void ReturnsNull_WhenNextMoveDoesNotSummon()
    {
        Assert.Null(Choose(Scratch, Bite, new[] { Bite }, new[] { Scratch, Bite, CallForBackup }));
    }

    [Fact]
    public void RepeatsLastPerformedMove_WhenNextMoveSummons()
    {
        Assert.Same(Bite, Choose(CallForBackup, Bite, new[] { Scratch }, new[] { Scratch, Bite, CallForBackup }));
    }

    [Fact]
    public void SkipsToFollowUp_WhenNoMoveWasPerformedYet()
    {
        Assert.Same(Bite, Choose(CallForBackup, null, new[] { Bite }, new[] { Scratch, Bite, CallForBackup }));
    }

    [Fact]
    public void SkipsSummoningFollowUps()
    {
        Assert.Same(Scratch, Choose(CallForBackup, null, new[] { Bloat, Scratch }, new[] { Bite, Scratch, CallForBackup, Bloat }));
    }

    [Fact]
    public void FallsBackToFirstNonSummonMove_WhenFollowUpsAllSummon()
    {
        Assert.Same(Bite, Choose(CallForBackup, null, new[] { Bloat }, new[] { CallForBackup, Bloat, Bite, Scratch }));
    }

    [Fact]
    public void IgnoresLastPerformedMove_WhenItSummoned()
    {
        Assert.Same(Scratch, Choose(CallForBackup, Bloat, new[] { Scratch }, new[] { Bite, Scratch, CallForBackup, Bloat }));
    }

    [Fact]
    public void ReturnsNull_WhenEveryMoveSummons()
    {
        Assert.Null(Choose(CallForBackup, null, new[] { Bloat }, new[] { CallForBackup, Bloat }));
    }

    [Fact]
    public void DoesNotEnumerateAlternatives_WhenNextMoveDoesNotSummon()
    {
        Move[] Throws() => throw new InvalidOperationException("alternatives must not be evaluated");

        Assert.Null(SummonGuard.ChooseReplacement(Scratch, null, Lazy(Throws), Lazy(Throws), move => move.Summons));
    }

    private static System.Collections.Generic.IEnumerable<Move> Lazy(Func<Move[]> source)
    {
        foreach (Move move in source())
        {
            yield return move;
        }
    }
}
