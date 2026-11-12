using System.Buffers.Binary;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Tests.IO;

/// <summary>
///     Tests for <see cref="Deobfuscator" />.
/// </summary>
/// <remarks>
///     Frostbite version is 0.0.0 because no profile is loaded. Apply version 1 header key.
/// </remarks>
internal sealed class DeobfuscatorTests
{
    private const int HeaderSize = 0x22C;

    // MEA trailer magic
    private static ReadOnlySpan<byte> TrailerMagic => "@e!adnXd$^!rfOsrDyIrI!xVgHeA!6Vc"u8;

    /// <summary>
    ///     Verify that a file without an obfuscation header is returned as is,
    ///     even when it is shorter than the header or the trailer.
    /// </summary>
    [TestCase(0)]
    [TestCase(10)]
    [TestCase(HeaderSize + 100)]
    public void Deobfuscate_ReturnsPlainFile(int length)
    {
        byte[] file = CreateBody(length);

        Assert.That(Deobfuscate(file), Is.EqualTo(file));
    }

    /// <summary>
    ///     Verify that version 1 header is removed.
    /// </summary>
    [TestCase((byte)0x01)]
    [TestCase((byte)0x03)]
    public void Deobfuscate_RemovesHeader(byte version)
    {
        byte[] header = CreateHeader(version);
        byte[] body = CreateBody(600);

        byte[] obfuscated = (byte[])body.Clone();
        if (version == 0x01)
        {
            for (int i = 0; i < obfuscated.Length; i++)
            {
                obfuscated[i] ^= (byte)(0x7B ^ header[0x128 + (i % 0x101)]);
            }
        }

        Assert.That(Deobfuscate([.. header, .. obfuscated]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that an obfuscated whole file (with MEA trailer) is decoded.
    /// </summary>
    /// <remarks>
    ///     The file is encoded as the inverse of Frosty v1's <c>MEADeobfuscator</c>,
    ///     which takes the initial key from the obfuscated trailer.
    /// </remarks>
    [Test]
    public void Deobfuscate_RemovesTrailerObfuscation()
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

        Assert.That(Deobfuscate([.. obfuscated, .. trailer]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that a file with MEA trailer but without obfuscation (magic 0 in the trailer)
    ///     returned without trailer and header, like v1 <c>MEADeobfuscator</c>.
    /// </summary>
    [Test]
    public void Deobfuscate_RemovesTrailerFromPlainFile()
    {
        byte[] body = CreateBody(600);
        byte[] trailer = CreateMeaTrailer(0, 0x5A, 0, out _);

        Assert.That(Deobfuscate([.. CreateHeader(0x03), .. body, .. trailer]), Is.EqualTo(body));
    }

    /// <summary>
    ///     Verify that a file with MEA trailer but does not have obfuscation header,
    ///     or the data is shorter than the header, throws NotImplemented
    /// </summary>
    [TestCase(10)]
    [TestCase(HeaderSize + 100)]
    public void Deobfuscate_ThrowsOnTrailerWithoutHeader(int length)
    {
        byte[] trailer = CreateMeaTrailer(4, 0x5A, 0, out _);

        Assert.That(() => Deobfuscate([.. CreateBody(length), .. trailer]), Throws.TypeOf<NotImplementedException>());
    }

    /// <summary>
    ///     Creates the trailer that v1 reads, in blocks obfuscated with their own key.
    /// </summary>
    /// <param name="magic">4 if the file is obfuscated, which v1 decodes with obfuscation type 2.</param>
    /// <param name="keySeed">The plain byte 5, which the initial key is derived from.</param>
    /// <param name="extraLength">The length of the optional block at 410.</param>
    /// <param name="initialValue">The key of the first byte of the file.</param>
    private static byte[] CreateMeaTrailer(uint magic, byte keySeed, short extraLength, out byte initialValue)
    {
        const int extraOffset = 410;
        int size = extraOffset + extraLength + sizeof(int) + TrailerMagic.Length;

        byte[] trailer = CreateBody(size);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer, magic);
        trailer[4] = 2;
        trailer[5] = keySeed;
        BinaryPrimitives.WriteInt16LittleEndian(trailer.AsSpan(392), extraLength);

        // v1 reads the length at 392 and byte 391 as they are, everything else here is obfuscated
        (int Offset, int Length)[] blocks = [(extraOffset, extraLength), (394, 4), (0, 4), (402, 4), (406, 4), (4, 387)];
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
        initialValue = (byte)(keySeed ^ total);

        BinaryPrimitives.WriteInt32LittleEndian(trailer.AsSpan(size - TrailerMagic.Length - sizeof(int)), size);
        TrailerMagic.CopyTo(trailer.AsSpan(size - TrailerMagic.Length));
        return trailer;
    }

    // The inverse of v1's MEADeobfuscator.DeobfuscateBlock, key depends on the plain bytes
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

    private static byte[] CreateHeader(byte version)
    {
        byte[] header = CreateBody(HeaderSize);
        header[0] = 0x00;
        header[1] = 0xD1;
        header[2] = 0xCE;
        header[3] = version;
        return header;
    }

    private static byte[] CreateBody(int length)
    {
        byte[] body = new byte[length];
        for (int i = 0; i < body.Length; i++)
        {
            body[i] = (byte)((i * 7) + 1);
        }

        return body;
    }

    private static byte[] Deobfuscate(byte[] file)
    {
        using MemoryStream stream = new(file, false);
        return Deobfuscator.Deobfuscate(stream);
    }
}
