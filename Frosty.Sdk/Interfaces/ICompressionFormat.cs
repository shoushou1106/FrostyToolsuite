using Frosty.Sdk.IO.Compression;

namespace Frosty.Sdk.Interfaces;

/// <summary>
///     A compression format of CAS blocks.
/// </summary>
public interface ICompressionFormat
{
    /// <summary>
    ///     Decompresses <paramref name="inData" /> into <paramref name="outData" />.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    /// <exception cref="InvalidDataException"><paramref name="inData" /> could not be decompressed.</exception>
    int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions options = CompressionOptions.None);

    /// <summary>
    ///     Compresses <paramref name="inData" /> into <paramref name="outData" />,
    ///     which must be at least <see cref="GetCompressBounds" /> bytes long.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    int Compress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions options = CompressionOptions.None);

    /// <summary>
    ///     Gets the largest size <see cref="Compress" /> can produce for <paramref name="rawSize" /> bytes.
    /// </summary>
    int GetCompressBounds(int rawSize, CompressionOptions options = CompressionOptions.None);
}