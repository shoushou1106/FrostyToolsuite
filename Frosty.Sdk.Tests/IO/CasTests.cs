using System.Buffers.Binary;
using System.Text;

using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.IO.Compression;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.IO;

/// <summary>
///     Tests for <see cref="Cas" /> block decompression and the managed compression formats behind it.
/// </summary>
internal sealed class CasTests
{
    private static readonly byte[] Plain =
        [.. "Frostbite CAS Test. OH NO! The Zombies broke into the EA server room and ate them. Unfortunately that means the servers are currently down. Please try again later."u8];

    // Large enough that Oodle compresses it instead of storing it
    private static readonly byte[] OodlePlain =
        [.. Enumerable.Range(0, 2048).SelectMany(static i => Encoding.UTF8.GetBytes(
            $"Frostbite CAS block {i}. The Zombies broke into the EA server room and ate them. Unfortunately that means the servers are currently down. Please try again later.\n"))];

    private static byte[] CompressWithOodle(byte[] inData, CompressionOptions inOptions)
    {
        CompressionOodle oodle = new();
        int bounds;
        try
        {
            bounds = oodle.GetCompressBounds(inData.Length, inOptions);
        }
        catch (DllNotFoundException)
        {
            Assert.Inconclusive("Skipping. Oodle is not downloaded to ThirdParty/ next to the tests");
            throw;
        }

        byte[] compressed = new byte[bounds];
        return compressed[..oodle.Compress(inData, compressed, inOptions)];
    }

    private static byte[] CreateBlock(CompressionType inType, int inDecompressedSize, ReadOnlySpan<byte> inPayload)
    {
        // flags (8) | decompressed size (24) | compression type (8) | magic 0x7 (4) | buffer size (20), big endian
        ulong packed = ((ulong)(uint)inDecompressedSize << 32) | ((ulong)(byte)inType << 24) | (0x7UL << 20) |
                       (uint)inPayload.Length;

        byte[] block = new byte[sizeof(ulong) + inPayload.Length];
        BinaryPrimitives.WriteUInt64BigEndian(block, packed);
        inPayload.CopyTo(block.AsSpan(sizeof(ulong)));
        return block;
    }

    private static byte[] DecompressBlocks(int inOriginalSize, params byte[][] inBlocks)
    {
        using DataStream stream = new(new MemoryStream([.. inBlocks.SelectMany(static b => b)]));
        using Block<byte> result = Cas.DecompressData(stream, inOriginalSize);
        return result.ToArray();
    }

    /// <summary>
    ///     Verify that blocks produced by the reference zlib, Zstd, and LZ4 encoders decode correctly.
    /// </summary>
    /// <remarks>
    ///     The payloads are encoded <see cref="Plain" />.
    /// </remarks>
    [TestCase(CompressionType.ZLib,
        "789c258c310ec2400c04bfb27ce0fe10211015412234740e317022674bb603caefb1a09ddd99bda9c75883b1edce18d8a3a03fe0d86f303c19576d6365c768fa6254094524de7570b6371b4cb58164026522a7567091bb5a2c92645e9351a03189ffccbfe62063dc163396c8d3a41f2938cd4c9e155b410faa82391356be168839cd")]
    [TestCase(CompressionType.Zstd,
        "28b52ffd20a3dd030042491b1a706bda406c71544a22a291c6c22d2f4131e843f9bfde25a0eabc28bfa67cc918e627d757bb3218dd03193f6aef998ec24fb3ead997d79af5a3be76ce262e0e61b4be66e6cc0b319d77e4d588c06fd6b897fc06e6627d5a1626fc46312029989018ad5ef10109f89b9655cc4103005a2558281b8c5b330a")]
    [TestCase(CompressionType.Lz4,
        "f36046726f7374626974652043415320546573742e204f48204e4f2120546865205a6f6d626965732062726f6b6520696e746f207468652045412073657276657220726f6f6d20616e6420617465207468656d2e20556e666f7274756e6174656c792074686174206d65616e73207468653700f01e73206172652063757272656e746c7920646f776e2e20506c656173652074727920616761696e206c617465722e")]
    public void DecompressData_ReadsReferenceEncoderOutput(CompressionType inType, string inPayloadHex)
    {
        byte[] block = CreateBlock(inType, Plain.Length, Convert.FromHexString(inPayloadHex));

        Assert.That(DecompressBlocks(Plain.Length, block), Is.EqualTo(Plain));
    }

    /// <summary>
    ///     Verify that each format round trips through several blocks, mixed with an uncompressed block.
    /// </summary>
    [TestCase(CompressionType.ZLib)]
    [TestCase(CompressionType.Zstd)]
    [TestCase(CompressionType.Lz4)]
    public void Compress_RoundTripsThroughCasBlocks(CompressionType inType)
    {
        ICompressionFormat format = inType switch
        {
            CompressionType.ZLib => new CompressionZLib(),
            CompressionType.Zstd => new CompressionZstd(),
            CompressionType.Lz4 => new CompressionLz4(),
            _ => throw new ArgumentOutOfRangeException(nameof(inType), inType, "How did you passed other compression type to this test?"),
        };

        byte[] compressed = new byte[format.GetCompressBounds(Plain.Length)];
        int compressedSize = format.Compress(Plain, compressed);
        byte[] block = CreateBlock(inType, Plain.Length, compressed.AsSpan(0, compressedSize));
        byte[] stored = CreateBlock(CompressionType.None, Plain.Length, Plain);

        Assert.That(DecompressBlocks(Plain.Length * 3, block, stored, block),
            Is.EqualTo<byte[]>([.. Plain, .. Plain, .. Plain]));
    }

    /// <summary>
    ///     Verify that each Oodle compressor shrinks and round-trips a CAS block, using the downloaded Oodle library.
    /// </summary>
    [TestCase(CompressionType.OodleKraken, CompressionOptions.OodleKraken)]
    [TestCase(CompressionType.OodleSelkie, CompressionOptions.OodleSelkie)]
    [TestCase(CompressionType.OodleLeviathan, CompressionOptions.OodleLeviathan)]
    public void Compress_RoundTripsOodleThroughCasBlock(CompressionType inType, CompressionOptions inOptions)
    {
        byte[] compressed = CompressWithOodle(OodlePlain, inOptions);
        byte[] block = CreateBlock(inType, OodlePlain.Length, compressed);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compressed, Has.Length.LessThan(OodlePlain.Length));
            Assert.That(DecompressBlocks(OodlePlain.Length, block), Is.EqualTo(OodlePlain));
        }
    }

    /// <summary>
    ///     Verify that a truncated Oodle block is rejected instead of being read past its end.
    /// </summary>
    [Test]
    public void DecompressData_RejectsTruncatedOodleBlock()
    {
        byte[] compressed = CompressWithOodle(OodlePlain, CompressionOptions.OodleKraken);
        byte[] block = CreateBlock(CompressionType.OodleKraken, OodlePlain.Length,
            compressed.AsSpan(0, compressed.Length / 2));

        Assert.Throws<InvalidDataException>(() => DecompressBlocks(OodlePlain.Length, block));
    }

    /// <summary>
    ///     Verify that a block decompressing to fewer bytes than its header claims is rejected.
    /// </summary>
    [Test]
    public void DecompressData_RejectsBlockShorterThanHeader()
    {
        byte[] block = CreateBlock(CompressionType.ZLib, Plain.Length + 1,
            Convert.FromHexString(
                "789c258c310ec2400c04bfb27ce0fe10211015412234740e317022674bb603caefb1a09ddd99bda9c75883b1edce18d8a3a03fe0d86f303c19576d6365c768fa6254094524de7570b6371b4cb58164026522a7567091bb5a2c92645e9351a03189ffccbfe62063dc163396c8d3a41f2938cd4c9e155b410faa82391356be168839cd"));

        Assert.Throws<InvalidDataException>(() => DecompressBlocks(Plain.Length + 1, block));
    }
}
