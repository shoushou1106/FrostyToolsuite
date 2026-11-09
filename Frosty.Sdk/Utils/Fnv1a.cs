namespace Frosty.Sdk.Utils;

/// <summary>
///     Frostbite FNV-1a string hash.
/// </summary>
/// <remarks>
///     Frostbite uses 5381 and 33 (the DJB2 constants), not the FNV offset basis and prime.
/// </remarks>
/// <seealso cref="Fnv1" />
// FNV-1a is the algorithm's name
// ReSharper disable once InconsistentNaming
public static class Fnv1a
{
    private const uint OffsetBasis = 5381;
    private const uint Prime = 33;

    /// <summary>
    ///     Hashes a string using Frostbite FNV-1a.
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
        foreach (char c in value)
        {
            hash ^= (byte)(toLower ? char.ToLowerInvariant(c) : c);
            hash *= Prime;
        }

        return (int)hash;
    }
}