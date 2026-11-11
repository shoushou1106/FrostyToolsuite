using System.Buffers.Binary;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Tests.IO;

/// <summary>
///     Tests for <see cref="ObfuscatedFile" />.
/// </summary>
/// <remarks>
///     No profile is loaded, so the Frostbite version is 0.0.0 and the version 1 header key applies.
///     TODO: These tests were heavily written by AI. Planned to revise.
/// </remarks>
internal sealed class ObfuscatedFileTests
{
    private const int HeaderSize = 0x22C;

    private static ReadOnlySpan<byte> TrailerMagic => "@e!adnXd$^!rfOsrDyIrI!xVgHeA!6Vc"u8;

    /// <summary>
    ///     Verify that a file without an obfuscation header is returned as is,
    ///     even when it is shorter than the header or the trailer.
    /// </summary>
    [TestCase(0)]
    [TestCase(10)]
    [TestCase(HeaderSize + 100)]
    public void ReadAllBytes_ReturnsPlainFile(int inLength)
    {
        byte[] file = CreateBody(inLength);

        Assert.That(ReadAllBytes(file), Is.EqualTo(file));
    }

    /// <summary>
    ///     Verify that the header is removed, and that version 1 removes the 257 byte key from the data.
    /// </summary>
    [TestCase((byte)0x01)]
    [TestCase((byte)0x03)]
    public void ReadAllBytes_RemovesHeader(byte inVersion)
    {
        byte[] header = CreateHeader(inVersion);
        byte[] body = CreateBody(600);

        byte[] obfuscated = (byte[])body.Clone();
        if (inVersion == 0x01)
        {
            for (int i = 0; i < obfuscated.Length; i++)
            {
                obfuscated[i] ^= (byte)(0x7B ^ header[0x128 + (i % 0x101)]);
            }
        }

        Assert.That(ReadAllBytes([.. header, .. obfuscated]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that a file obfuscated as a whole, with the trailer of Mass Effect Andromeda, is decoded.
    /// </summary>
    /// <remarks>
    ///     The file is encoded as the inverse of Frosty v1's <c>MEADeobfuscator</c>, which takes the initial key
    ///     from the obfuscated trailer.
    /// </remarks>
    [Test]
    public void ReadAllBytes_RemovesTrailerObfuscation()
    {
        byte[] body = CreateBody(600);
        byte[] plain = [.. CreateHeader(0x03), .. body];
        byte[] trailer = CreateMeaTrailer(4, 0x5A, 13, out byte initialValue);

        byte[] obfuscated = new byte[plain.Length];
        byte key = initialValue;
        for (int i = 0; i < plain.Length; i++)
        {
            obfuscated[i] = (byte)(plain[i] ^ key);
            key = (byte)((initialValue ^ obfuscated[i]) - i);
        }

        Assert.That(ReadAllBytes([.. obfuscated, .. trailer]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that a file with the trailer but without obfuscation (magic 0 in the trailer) loses the trailer and
    ///     the header, like in v1.
    /// </summary>
    [Test]
    public void ReadAllBytes_RemovesTrailerFromPlainFile()
    {
        byte[] body = CreateBody(600);
        byte[] trailer = CreateMeaTrailer(0, 0x5A, 0, out _);

        Assert.That(ReadAllBytes([.. CreateHeader(0x03), .. body, .. trailer]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that a file with the trailer whose data has no obfuscation header throws, also when it is shorter
    ///     than the header.
    /// </summary>
    [TestCase(10)]
    [TestCase(HeaderSize + 100)]
    public void ReadAllBytes_ThrowsOnTrailerWithoutHeader(int inLength)
    {
        byte[] trailer = CreateMeaTrailer(4, 0x5A, 0, out _);

        Assert.That(() => ReadAllBytes([.. CreateBody(inLength), .. trailer]), Throws.TypeOf<NotImplementedException>());
    }

    /// <summary>
    ///     Creates the trailer that v1 reads, in blocks obfuscated with their own key.
    /// </summary>
    /// <param name="inMagic">4 if the file is obfuscated, which v1 then decodes with obfuscation type 2.</param>
    /// <param name="inKeySeed">The plain byte 5, which the initial key is derived from.</param>
    /// <param name="inExtraLength">The length of the optional block at 410.</param>
    /// <param name="outInitialValue">The key of the first byte of the file.</param>
    private static byte[] CreateMeaTrailer(uint inMagic, byte inKeySeed, short inExtraLength, out byte outInitialValue)
    {
        const int extraOffset = 410;
        int size = extraOffset + inExtraLength + sizeof(int) + TrailerMagic.Length;

        byte[] trailer = CreateBody(size);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer, inMagic);
        trailer[4] = 2;
        trailer[5] = inKeySeed;
        BinaryPrimitives.WriteInt16LittleEndian(trailer.AsSpan(392), inExtraLength);

        // v1 reads the length at 392 and byte 391 as they are, everything else here is obfuscated
        (int Offset, int Length)[] blocks = [(extraOffset, inExtraLength), (394, 4), (0, 4), (402, 4), (406, 4), (4, 387)];
        foreach ((int offset, int length) in blocks)
        {
            EncodeTrailerBlock(trailer.AsSpan(offset, length));
        }

        // The total is taken over the obfuscated bytes
        uint total = 0;
        foreach ((int offset, int length) in blocks)
        {
            total += GetTrailerChecksum(trailer.AsSpan(offset, length));
        }

        total += GetTrailerChecksum(trailer.AsSpan(391, 1));
        outInitialValue = (byte)(inKeySeed ^ total);

        BinaryPrimitives.WriteInt32LittleEndian(trailer.AsSpan(size - TrailerMagic.Length - sizeof(int)), size);
        TrailerMagic.CopyTo(trailer.AsSpan(size - TrailerMagic.Length));
        return trailer;
    }

    // The inverse of v1's MEADeobfuscator.DeobfuscateBlock, whose key depends on the plain bytes
    private static void EncodeTrailerBlock(Span<byte> bytes)
    {
        int a = 1172968056;
        int z = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            int b = bytes[i];
            bytes[i] = (byte)(b ^ (a + ((a >> 8) & 0xFF) + (a >> 16) + ((a >> 24) & 0xFF)));

            int c = int.RotateLeft(a, b & 0x1F);
            a = int.RotateLeft((b | ((b | ((b | (b << 8)) << 8)) << 8)) + c, 1);

            if (z > 16)
            {
                a *= 2;
                z = 0;
            }

            z++;
        }
    }

    private static uint GetTrailerChecksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        foreach (byte x in bytes)
        {
            sum = x ^ (2 * (x + sum));
        }

        return sum;
    }

    private static byte[] CreateHeader(byte inVersion)
    {
        byte[] header = CreateBody(HeaderSize);
        header[0] = 0x00;
        header[1] = 0xD1;
        header[2] = 0xCE;
        header[3] = inVersion;
        return header;
    }

    private static byte[] CreateBody(int inLength)
    {
        byte[] body = new byte[inLength];
        for (int i = 0; i < body.Length; i++)
        {
            body[i] = (byte)((i * 7) + 1);
        }

        return body;
    }

    private static byte[] ReadAllBytes(byte[] inFile)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, inFile);
            return ObfuscatedFile.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
