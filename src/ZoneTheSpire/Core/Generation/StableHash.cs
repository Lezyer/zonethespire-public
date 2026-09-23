using System.Globalization;
using System.Text;

namespace ZoneTheSpire.Core.Generation;

/// <summary>
/// Process- and platform-stable hashing. Never use string.GetHashCode() for anything that must match
/// across game sessions or multiplayer peers: it is randomized per process.
/// </summary>
public static class StableHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>64-bit FNV-1a over the UTF-8 bytes of <paramref name="value"/>.</summary>
    public static ulong Fnv1a64(string value)
    {
        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash;
    }

    /// <summary>Folds a 64-bit hash to 32 bits for compact log output.</summary>
    public static uint Fold32(ulong hash) => (uint)(hash ^ (hash >> 32));

    /// <summary>Culture-invariant integer formatting for canonical hash input strings.</summary>
    public static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
