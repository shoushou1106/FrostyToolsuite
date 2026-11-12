using Frosty.Sdk.IO.Compression;

namespace Frosty.Sdk.Interfaces;

/// <summary>
///     A compression format of CAS blocks.
/// </summary>
public interface ICompressionFormat
{
    /// <summary>
    ///     The name of the format for messages
    /// </summary>
    /// <example>Zstandard, Oodle</example>
    string Identifier { get; }

    /// <summary>
    ///     Decompresses <paramref name="inData" /> into <paramref name="outData" />.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    /// <exception cref="InvalidDataException"><paramref name="inData" /> could not be decompressed.</exception>
    int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None);

    /// <summary>
    ///     Compresses <paramref name="inData" /> into <paramref name="outData" />,
    ///     which must be at least <see cref="GetCompressBounds" /> bytes long.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    int Compress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None);

    /// <summary>
    ///     Gets the largest size <see cref="Compress" /> can produce for <paramref name="inRawSize" /> bytes.
    /// </summary>
    int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None);
}
