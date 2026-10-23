using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Frosty.Sdk.Utils;

/// <summary>
///     A 20-byte SHA-1 hash.
/// </summary>
public readonly struct Sha1 : IEquatable<Sha1>
{
    private const int Size = SHA1.HashSizeInBytes;

    public static Sha1 Zero => default;

    private readonly uint _a, _b, _c, _d, _e;

    /// <summary>
    ///     Creates a hash from its first 20 bytes.
    /// </summary>
    /// <param name="bytes">The hash bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="bytes" /> is shorter than 20 bytes.</exception>
    public Sha1(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
        {
            throw new ArgumentException("Input buffer is too small", nameof(bytes));
        }

        _a = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        _b = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        _c = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        _d = BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        _e = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
    }

    /// <summary>
    ///     Creates a hash from its hex string, as returned by <see cref="ToString" />.
    /// </summary>
    /// <param name="text">The hex string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    /// <exception cref="FormatException"><paramref name="text" /> is not a hex string.</exception>
    /// <exception cref="ArgumentException"><paramref name="text" /> is shorter than 20 bytes.</exception>
    public Sha1(string text) : this(Convert.FromHexString(text))
    {
    }

    /// <summary>
    ///     Computes the SHA-1 hash of <paramref name="source" />.
    /// </summary>
    /// <param name="source">The data to hash.</param>
    /// <returns>The hash.</returns>
    public static Sha1 HashData(ReadOnlySpan<byte> source)
    {
        Span<byte> hash = stackalloc byte[Size];
        // CA5350: Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5350
        SHA1.HashData(source, hash);
#pragma warning restore CA5350
        return new Sha1(hash);
    }

    public static bool operator ==(Sha1 a, Sha1 b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Sha1 a, Sha1 b)
    {
        return !a.Equals(b);
    }

    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[Size];
        TryWriteBytes(bytes);
        return bytes;
    }

    public bool TryWriteBytes(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, _a);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], _b);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], _c);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], _d);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], _e);
        return true;
    }

    public bool Equals(Sha1 other)
    {
        return _a == other._a && _b == other._b && _c == other._c && _d == other._d && _e == other._e;
    }

    public override bool Equals(object? obj)
    {
        return obj is Sha1 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_a, _b, _c, _d, _e);
    }

    /// <summary>
    ///     Returns the hash as a lowercase hex string.
    /// </summary>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[Size];
        TryWriteBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}