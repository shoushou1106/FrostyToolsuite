using Frosty.Sdk.Interfaces;

using K4os.Compression.LZ4;

namespace Frosty.Sdk.IO.Compression;

/// <summary>
///     LZ4 block format (no framing)
/// </summary>
public sealed class CompressionLz4 : ICompressionFormat
{
    public string Identifier => "LZ4";

    public int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        int written = LZ4Codec.Decode(inData, outData);
        return written >= 0 ? written : throw new InvalidDataException("An LZ4 block could not be decompressed.");
    }

    public int Compress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        int written = LZ4Codec.Encode(inData, outData);
        return written >= 0 ? written : throw new ArgumentException("The destination is too small.", nameof(outData));
    }

    public int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None)
    {
        return LZ4Codec.MaximumOutputSize(inRawSize);
    }
}