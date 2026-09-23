using System;

namespace ZoneTheSpire.Core.Generation;

/// <summary>
/// SplitMix64 PRNG. Self-contained so results never change with game RNG updates and never consume the
/// run's shared RNG streams (which would desync vanilla rolls in multiplayer).
/// </summary>
public sealed class ZoneRandom
{
    private ulong _state;

    public ZoneRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>A named, independent stream derived from the run seed.</summary>
    public static ZoneRandom ForStream(ulong runSeed, string streamName) =>
        new(runSeed ^ StableHash.Fnv1a64(streamName));

    public ulong NextULong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform-enough integer in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be positive.");
        }

        return (int)(NextULong() % (ulong)maxExclusive);
    }

    /// <summary>Integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be lower than maximum.");
        }

        return minInclusive + NextInt(maxExclusive - minInclusive);
    }
}
