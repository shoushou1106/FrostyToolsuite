using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.IO;

// ReSharper disable AccessToDisposedClosure
// Assertion delegates run synchronously before disposing captured streams.

/// <summary>
///     Tests for <see cref="DataStream" /> and <see cref="BlockStream" /> string handling.
/// </summary>
internal sealed class DataStreamTests
{
    /// <summary>
    ///     Verify that every string format round trips non-ASCII text.
    /// </summary>
    [TestCase("Frosty Magic")]
    [TestCase("❄ ❄️")]
    [TestCase("Magie givrée")]
    [TestCase("Морозное волшебство")]
    [TestCase("フロスティマジック")]
    [TestCase("寒霜魔法")]
    public void Strings_RoundTripNonAscii(string inText)
    {
        using DataStream stream = new(new MemoryStream());
        stream.WriteNullTerminatedString(inText);
        stream.WriteSizedString(inText);
        stream.WriteFixedSizedString(inText, 64);
        stream.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream.ReadNullTerminatedString(), Is.EqualTo(inText));
            Assert.That(stream.ReadSizedString(), Is.EqualTo(inText));
            Assert.That(stream.ReadFixedSizedString(64), Is.EqualTo(inText));
            Assert.That(stream.Position, Is.EqualTo(stream.Length));
        }
    }

    /// <summary>
    ///     Verify that the in-memory string read of <see cref="BlockStream" /> matches the byte by byte one.
    /// </summary>
    [Test]
    public void BlockStream_ReadNullTerminatedString_MatchesDataStream()
    {
        using Block<byte> data = new("Frosty Magic ❄️\0Magie givrée ❄️\0寒霜魔法 ❄️\0"u8);
        using BlockStream stream = new(data, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream.ReadNullTerminatedString(), Is.EqualTo("Frosty Magic ❄️"));
            Assert.That(stream.ReadNullTerminatedString(), Is.EqualTo("Magie givrée ❄️"));
            Assert.That(stream.ReadNullTerminatedString(), Is.EqualTo("寒霜魔法 ❄️"));
            Assert.Throws<EndOfStreamException>(() => stream.ReadNullTerminatedString());
        }
    }

    /// <summary>
    ///     Verify that single byte chars map bytes to Latin-1 and refuse chars that need more than one byte.
    /// </summary>
    [Test]
    public void Latin1Char_IsOneBytePerChar()
    {
        using DataStream stream = new(new MemoryStream());
        stream.WriteLatin1Char('A');
        stream.WriteLatin1Char('é');
        stream.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream.ReadBytes(2), Is.EqualTo(new byte[] { 0x41, 0xE9 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.WriteLatin1Char('寒'));
        }

        stream.Position = 1;
        Assert.That(stream.ReadLatin1Char(), Is.EqualTo('é'));
    }

    /// <summary>
    ///     Verify that disposing leaves the underlying stream usable only when asked.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Dispose_ClosesStreamUnlessLeftOpen(bool inLeaveOpen)
    {
        using MemoryStream inner = new();
        new DataStream(inner, inLeaveOpen).Dispose();

        Assert.That(inner.CanRead, Is.EqualTo(inLeaveOpen));
    }

    /// <summary>
    ///     Verify that disposing more than once is harmless, as <see cref="IDisposable" /> requires, and the block stays trimmed.
    /// </summary>
    [Test]
    public void BlockStream_DisposeTwice_KeepsTrimmedBlock()
    {
        using Block<byte> block = new(0);
        BlockStream stream = new(block, true);
        stream.WriteInt32(42);
        stream.Dispose();

        Assert.DoesNotThrow(stream.Dispose);
        Assert.DoesNotThrow(stream.Dispose);
        Assert.That(block.Size, Is.EqualTo(sizeof(int)));
    }

    /// <summary>
    ///     Verify that big endian guids keep the byte order Frostbite uses.
    /// </summary>
    [Test]
    public void Guid_RoundTripsBothEndians()
    {
        Guid guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        using DataStream stream = new(new MemoryStream());
        stream.WriteGuid(guid, Endian.Big);
        stream.WriteGuid(guid);
        stream.Position = 0;

        byte[] bigEndian = stream.ReadBytes(16);
        stream.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Convert.ToHexStringLower(bigEndian), Is.EqualTo("00112233445566778899aabbccddeeff"));
            Assert.That(stream.ReadGuid(Endian.Big), Is.EqualTo(guid));
            Assert.That(stream.ReadGuid(), Is.EqualTo(guid));
        }
    }
}
