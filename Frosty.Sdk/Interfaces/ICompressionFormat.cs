using Frosty.Sdk.IO.Compression;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Interfaces;

public interface ICompressionFormat
{
    string Identifier { get; }

    /// <summary>
    ///     Decompresses <paramref name="inData" /> and writes it to <paramref name="outData" />.
    /// </summary>
    void Decompress<T>(Block<T> inData, ref Block<T> outData, CompressionFlags inFlags = CompressionFlags.None)
        where T : unmanaged;

    /// <summary>
    ///     Compresses <paramref name="inData" /> and writes it to <paramref name="outData" />.
    /// </summary>
    int Compress<T>(Block<T> inData, ref Block<T> outData, CompressionFlags inFlags = CompressionFlags.None)
        where T : unmanaged;

    int GetCompressBounds(int inRawSize, CompressionFlags inFlags = CompressionFlags.None);
}