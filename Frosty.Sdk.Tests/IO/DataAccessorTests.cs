using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.IO;

// ReSharper disable AccessToDisposedClosure
// Assertion delegates run synchronously before disposing captured accessors.

/// <summary>
///     Tests for <see cref="DataAccessor" /> and <see cref="BlockAccessor" /> string handling.
/// </summary>
internal sealed class DataAccessorTests
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
        using DataAccessor accessor = new(new MemoryStream());
        accessor.WriteNullTerminatedString(inText);
        accessor.WriteSizedString(inText);
        accessor.WriteFixedSizedString(inText, 64);
        accessor.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accessor.ReadNullTerminatedString(), Is.EqualTo(inText));
            Assert.That(accessor.ReadSizedString(), Is.EqualTo(inText));
            Assert.That(accessor.ReadFixedSizedString(64), Is.EqualTo(inText));
            Assert.That(accessor.Position, Is.EqualTo(accessor.Length));
        }
    }

    /// <summary>
    ///     Verify that the in-memory string read of <see cref="BlockAccessor" /> matches the byte by byte one.
    /// </summary>
    [Test]
    public void BlockAccessor_ReadNullTerminatedString_MatchesDataAccessor()
    {
        using Block<byte> data = new("Frosty Magic ❄️\0Magie givrée ❄️\0寒霜魔法 ❄️\0"u8);
        using BlockAccessor accessor = new(data, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accessor.ReadNullTerminatedString(), Is.EqualTo("Frosty Magic ❄️"));
            Assert.That(accessor.ReadNullTerminatedString(), Is.EqualTo("Magie givrée ❄️"));
            Assert.That(accessor.ReadNullTerminatedString(), Is.EqualTo("寒霜魔法 ❄️"));
            Assert.Throws<EndOfStreamException>(() => accessor.ReadNullTerminatedString());
        }
    }

    /// <summary>
    ///     Verify that single byte chars map bytes to Latin-1 and refuse chars that need more than one byte.
    /// </summary>
    [Test]
    public void Latin1Char_IsOneBytePerChar()
    {
        using DataAccessor accessor = new(new MemoryStream());
        accessor.WriteLatin1Char('A');
        accessor.WriteLatin1Char('é');
        accessor.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(accessor.ReadBytes(2), Is.EqualTo(new byte[] { 0x41, 0xE9 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => accessor.WriteLatin1Char('寒'));
        }

        accessor.Position = 1;
        Assert.That(accessor.ReadLatin1Char(), Is.EqualTo('é'));
    }

    /// <summary>
    ///     Verify that disposing leaves the underlying stream usable only when asked.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Dispose_ClosesStreamUnlessLeftOpen(bool inLeaveOpen)
    {
        using MemoryStream inner = new();
        new DataAccessor(inner, inLeaveOpen).Dispose();

        Assert.That(inner.CanRead, Is.EqualTo(inLeaveOpen));
    }

    /// <summary>
    ///     Verify that disposing more than once is harmless, as <see cref="IDisposable" /> requires, and the block stays
    ///     trimmed.
    /// </summary>
    [Test]
    public void BlockAccessor_DisposeTwice_KeepsTrimmedBlock()
    {
        using Block<byte> block = new(0);
        BlockAccessor accessor = new(block, true);
        accessor.WriteInt32(42);
        accessor.Dispose();

        Assert.DoesNotThrow(accessor.Dispose);
        Assert.DoesNotThrow(accessor.Dispose);
        Assert.That(block.Size, Is.EqualTo(sizeof(int)));
    }

    /// <summary>
    ///     Verify that big endian guids keep the byte order Frostbite uses.
    /// </summary>
    [Test]
    public void Guid_RoundTripsBothEndians()
    {
        Guid guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        using DataAccessor accessor = new(new MemoryStream());
        accessor.WriteGuid(guid, Endian.Big);
        accessor.WriteGuid(guid);
        accessor.Position = 0;

        byte[] bigEndian = accessor.ReadBytes(16);
        accessor.Position = 0;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Convert.ToHexStringLower(bigEndian), Is.EqualTo("00112233445566778899aabbccddeeff"));
            Assert.That(accessor.ReadGuid(Endian.Big), Is.EqualTo(guid));
            Assert.That(accessor.ReadGuid(), Is.EqualTo(guid));
        }
    }
}