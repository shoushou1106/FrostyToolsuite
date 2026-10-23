namespace Frosty.Sdk.Utils;

/// <summary>
///     Frostbite FNV-1 string hash.
/// </summary>
/// <remarks>
///     Formerly Utils.Utils.HashString()
/// </remarks>
/// <seealso cref="Fnv1a" />
public static class Fnv1
{
    private const uint OffsetBasis = 5381;
    private const uint Prime = 33;

    /// <summary>
    ///     Hashes a string using Frostbite FNV-1.
    /// </summary>
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