using System.IO.Compression;

using Frosty.Sdk.Interfaces;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.Compression;

/// <summary>
///     Zstandard format
/// </summary>
public sealed class CompressionZstd : ICompressionFormat
{
    private static readonly Lazy<ZstandardDictionary> EbxDictionary = new(LoadEbxDictionary);

    public string Identifier => "ZStandard";

    public int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        bool success = inOptions.HasFlag(CompressionOptions.ZstdWithDict)
            ? ZstandardDecoder.TryDecompress(inData, outData, out int written, EbxDictionary.Value)
            : ZstandardDecoder.TryDecompress(inData, outData, out written);

        return success ? written : throw new InvalidDataException("A Zstandard block could not be decompressed.");
    }

    public int Compress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        return ZstandardEncoder.TryCompress(inData, outData, out int written)
            ? written
            : throw new ArgumentException("The destination is too small.", nameof(outData));
    }

    public int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None)
    {
        return checked((int)ZstandardEncoder.GetMaxCompressedLength(inRawSize));
    }

    private static ZstandardDictionary LoadEbxDictionary()
    {
        // Do not dispose, the block belongs to the memory fs. Create() copies the data.
        Block<byte> dictionary = FileSystemManager.GetFileFromMemoryFs("Dictionaries/ebx.dict");
        return ZstandardDictionary.Create(dictionary);
    }
}