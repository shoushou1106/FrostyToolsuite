using System.IO.Compression;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.IO.Compression;

/// <summary>
///     zlib format (deflate with the zlib header and checksum)
/// </summary>
public sealed class CompressionZLib : ICompressionFormat
{
    public string Identifier => "ZLib";

    public int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None)
    {
        return ZLibDecoder.TryDecompress(inData, outData, out int written)
            ? written
            : throw new InvalidDataException("A zlib block could not be decompressed.");
    }

    public int Compress(ReadOnlySpan<byte> inData, Span<byte> outData, CompressionOptions inOptions = CompressionOptions.None)
    {
        return ZLibEncoder.TryCompress(inData, outData, out int written)
            ? written
            : throw new ArgumentException("The destination is too small.", nameof(outData));
    }

    public int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None)
    {
        return checked((int)ZLibEncoder.GetMaxCompressedLength(inRawSize));
    }
}
