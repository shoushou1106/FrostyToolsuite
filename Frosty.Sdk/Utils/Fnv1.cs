namespace Frosty.Sdk.Utils;

/// <summary>
///     Frostbite FNV-1 string hash.
/// </summary>
/// <remarks>
///     Frostbite uses DJB2 constants (starting seed 5381 and the multiplier 33), not the FNV offset basis and prime.
/// </remarks>
/// <seealso cref="Fnv1a" />
public static class Fnv1
{
    private const uint OffsetBasis = 5381;
    private const uint Prime = 33;

    /// <summary>
    ///     Hashes a string using Frostbite FNV-1.
    /// </summary>
    /// <remarks>
    ///     Only the low byte of each char is hashed,
    ///     so a non-ASCII string does not hash like its UTF-8 bytes.
    /// </remarks>
    /// <param name="value">The string to hash.</param>
    /// <param name="toLower">Whether to lowercase before hashing.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
    public static int HashString(string value, bool toLower = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        uint hash = OffsetBasis;
        // Hot path, keep it a plain loop instead of LINQ
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (char c in value)
        {
            hash = (hash * Prime) ^ (byte)(toLower ? char.ToLowerInvariant(c) : c);
        }

        return (int)hash;
    }
}