namespace Frosty.Sdk.Utils;

public static class GuidExtensions
{
    // CA1034: Do not nest type
#pragma warning disable CA1034
    extension(Guid guid)
    {
        /// <summary>
        ///     Compares two guids byte by byte in the order <see cref="Guid.TryWriteBytes(Span{byte})" /> writes them.
        /// </summary>
        /// <param name="other">The guid to compare with.</param>
        /// <returns>A negative number, zero, or a positive number like <see cref="IComparable{T}.CompareTo" />.</returns>
        public int CompareToBigEndian(Guid other)
        {
            Span<byte> a = stackalloc byte[16];
            Span<byte> b = stackalloc byte[16];
            guid.TryWriteBytes(a);
            other.TryWriteBytes(b);

            return a.SequenceCompareTo(b);
        }
    }
#pragma warning restore CA1034
}