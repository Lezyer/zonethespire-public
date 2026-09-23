using System;
using Godot;
using MegaCrit.Sts2.Core.Commands;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Forgotten Empire sounds: the block break sound when Marbled takes a hit, and the block gain sound when Marbled is added by
/// Polishing. When several creatures gain at once (every enemy at the end of their turn) the sound plays once rather than
/// stacking.
/// Local audio only; never affects gameplay.
/// </summary>
internal static class MarbleSounds
{
    private const string HitSound = "event:/sfx/block_break";
    private const string GrowSound = "event:/sfx/block_gain";
    private const ulong GrowThrottleMs = 250;

    private static ulong _lastGrowMs;

    public static void Hit() => Play(HitSound);

    public static void Grow()
    {
        ulong now = Time.GetTicksMsec();
        if (_lastGrowMs != 0 && now - _lastGrowMs < GrowThrottleMs)
        {
            return;
        }

        _lastGrowMs = now;
        Play(GrowSound);
    }

    private static void Play(string sound)
    {
        try
        {
            SfxCmd.Play(sound);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to play a Marbled sound: {ex}");
        }
    }
}
