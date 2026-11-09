namespace Frosty.Sdk.Utils;

public static class GuidExtensions
{
    extension(Guid guid)
    {
        /// <summary>
        ///     Compares two guids byte by byte in the order <see cref="Guid.TryWriteBytes(Span{byte})" /> writes them.
        /// </summary>
        /// <remarks>
        ///     Compare the mixed-endian layout,
        ///     so the order differs from <see cref="Guid.CompareTo(Guid)" />,
        ///     which compares the first three fields as numbers.
        /// </remarks>
        /// <param name="other">The GUID to compare with.</param>
        /// <returns>A negative number, zero, or a positive number like <see cref="IComparable{T}.CompareTo" />.</returns>
        public int CompareByBytesTo(Guid other)
        {
            Span<byte> a = stackalloc byte[16];
            Span<byte> b = stackalloc byte[16];
            guid.TryWriteBytes(a);
            other.TryWriteBytes(b);

            return a.SequenceCompareTo(b);
        }
    }
}