using Frosty.Sdk.IO.Compression;

namespace Frosty.Sdk.Interfaces;

public interface ICompressionFormat
{
    string Identifier { get; }

    /// <summary>
    ///     Decompresses <paramref name="inData" /> into <paramref name="outData" />.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    /// <exception cref="InvalidDataException"><paramref name="inData" /> could not be decompressed.</exception>
    int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None);

    /// <summary>
    ///     Compresses <paramref name="inData" /> into <paramref name="outData" />,
    ///     which promised to be at least <see cref="GetCompressBounds" /> bytes long.
    /// </summary>
    /// <returns>The number of bytes written to <paramref name="outData" />.</returns>
    int Compress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None);

    /// <summary>
    ///     Get the largest size <see cref="Compress" /> can produce for <paramref name="inRawSize" /> bytes.
    /// </summary>
    int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None);
}
