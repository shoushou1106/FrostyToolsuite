using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

/// <summary>
///     A <see cref="DataAccessor" /> over a <see cref="Block{T}" /> in memory, which grows when written past its end.
/// </summary>
public class BlockAccessor : DataAccessor
{
    private readonly Block<byte> _block;
    private readonly bool _leaveOpen;
    private bool _disposed;

    public BlockAccessor()
    {
        _block = new Block<byte>(0);
        BaseStream = _block.ToStream();
    }

    public BlockAccessor(int inSize)
    {
        _block = new Block<byte>(inSize);
        BaseStream = _block.ToStream();
    }

    public BlockAccessor(Block<byte> inBuffer, bool inLeaveOpen = false)
    {
        _block = inBuffer;
        BaseStream = _block.ToStream();
        _leaveOpen = inLeaveOpen;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ResizeStream(Position + buffer.Length);
        base.Write(buffer);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ResizeStream(Position + count);
        base.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        ResizeStream(Position + sizeof(byte));
        base.WriteByte(value);
    }

    public override void CopyTo(DataAccessor destination, int bufferSize)
    {
        if (destination is not BlockAccessor accessor)
        {
            base.CopyTo(destination, bufferSize);
            return;
        }

        if (bufferSize <= 0)
        {
            return;
        }

        accessor.ResizeStream(accessor.Position + bufferSize);

        if (accessor.Length < accessor.Position + bufferSize)
        {
            accessor.BaseStream.SetLength((int)accessor.Position + bufferSize);
        }

        using Block<byte> source = new(_block, (int)Position, bufferSize);
        using Block<byte> dest = new(accessor._block, (int)accessor.Position, bufferSize);
        source.CopyTo(dest);

        accessor.Position += bufferSize;

        Position += bufferSize;
    }

    /// <summary>
    ///     Same as <see cref="DataAccessor.ReadNullTerminatedString(Encoding)" />,
    ///     but finds the terminator in memory instead of reading byte by byte.
    /// </summary>
    /// <remarks>
    ///     <b>Encoding:</b> Any ASCII-compatible <paramref name="inEncoding" />.
    ///     The UTF-8 <see cref="DataAccessor.ReadNullTerminatedString()" /> calls this override too.
    /// </remarks>
    public override string ReadNullTerminatedString(Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(inEncoding);

        // All the 8-bit encoding, for example, UTF-8, uses high-order bytes,
        // that all chars are promised to be higher than 0x80.
        // So there is nothing needed to degrade into base.ReadNullTerminatedString.
        // Non-ASCII-compatible encodings like UTF-16 and UTF-32 are not supported,
        // but the base method does not support them either.
        
        // "Length - Position" prevent reading trash from an uninitialized Block
        ReadOnlySpan<byte> remaining = _block.ToReadOnlySpan((int)Position, (int)(Length - Position));
        int length = remaining.IndexOf((byte)0);

        if (length == -1)
        {
            throw new EndOfStreamException();
        }

        Position += length + 1;
        return inEncoding.GetString(remaining[..length]);
    }

    public override DataAccessor CreateSubAccessor(long inStartOffset, int inSize)
    {
        Block<byte> sub = new(_block, (int)inStartOffset, inSize);
        return new BlockAccessor(sub);
    }

    /// <summary>
    /// Loads the whole file into memory and deobfuscates it if necessary.
    /// </summary>
    /// <param name="inPath">The path of the file</param>
    /// <param name="inShouldDeobfuscate">The boolean if the file needs to be deobfuscated</param>
    /// <returns>A <see cref="BlockAccessor"/> that has the file loaded.</returns>
    public static BlockAccessor FromFile(string inPath, bool inShouldDeobfuscate)
    {
        using FileStream stream = new(inPath, FileMode.Open, FileAccess.Read);

        if (inShouldDeobfuscate)
        {
            // ReSharper disable once GrammarMistakeInComment
            // The deobfuscated accessor is returned to the caller.
            // CA2000: Dispose objects before losing scope
#pragma warning disable CA2000
            BlockAccessor? deobfuscated = CheckExtraObfuscation(stream, out int keySize)
                ? DeobfuscateWithKey(stream, keySize)
                : Deobfuscate(stream);
#pragma warning restore CA2000
            if (deobfuscated is not null)
            {
                return deobfuscated;
            }

            stream.Position = 0;
        }

        BlockAccessor retVal = new(Block<byte>.CreateUninitialized((int)stream.Length));
        stream.ReadExactly(retVal._block);
        return retVal;
    }

    /// <summary>
    /// Loads part of a file into memory.
    /// </summary>
    /// <param name="inPath">The path of the file.</param>
    /// <param name="inOffset">The offset of the data to load.</param>
    /// <param name="inSize">The size of the data to load</param>
    /// <returns>A <see cref="BlockAccessor"/> that has the data loaded.</returns>
    public static BlockAccessor FromFile(string inPath, long inOffset, int inSize)
    {
        using FileStream stream = new(inPath, FileMode.Open, FileAccess.Read);
        stream.Position = inOffset;

        BlockAccessor retVal = new(Block<byte>.CreateUninitialized(inSize));
        stream.ReadExactly(retVal._block);
        return retVal;
    }

    /// <summary>
    /// <see cref="Aes"/> decrypt this <see cref="BlockAccessor"/>.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode"/> to use for the decryption.</param>
    public void Decrypt(byte[] inKey, PaddingMode inPaddingMode)
    {
        using Aes aes = Aes.Create();
        aes.Key = inKey;
        Span<byte> span = _block.ToSpan((int)Position);
        aes.DecryptCbc(span, inKey, span, inPaddingMode);
    }

    /// <summary>
    /// <see cref="Aes"/> decrypt part of this <see cref="BlockAccessor"/>.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inSize">The size of the data to decrypt.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode"/> to use for the decryption.</param>
    public void Decrypt(byte[] inKey, int inSize, PaddingMode inPaddingMode)
    {
        using Aes aes = Aes.Create();
        aes.Key = inKey;
        Span<byte> span = _block.ToSpan((int)Position, inSize);
        aes.DecryptCbc(span, inKey, span, inPaddingMode);
    }

    /// <remarks>
    ///     When the block is left open, it is trimmed (or zero padded) to exactly what was written.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        // Trimming touches the stream, so it must only run on the first call
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_leaveOpen)
            {
                TrimBlockToLength();
            }
            else
            {
                _block.Dispose();
            }
        }

        base.Dispose(disposing);
        // The base already calls GC.SuppressFinalize(this)
    }

    private static bool CheckExtraObfuscation(Stream inStream, out int keySize)
    {
        keySize = 0;

        // Read signature
        inStream.Seek(-36, SeekOrigin.End);
        Span<byte> signature = stackalloc byte[36];
        inStream.ReadExactly(signature);
        inStream.Position = 0;

        // Check signature
        if (!signature[4..].SequenceEqual("@e!adnXd$^!rfOsrDyIrI!xVgHeA!6Vc"u8))
        {
            return false;
        }

        // Get key size
        keySize = BinaryPrimitives.ReadInt32LittleEndian(signature);
        return true;
    }

    private static BlockAccessor? DeobfuscateWithKey(Stream inStream, int inKeySize)
    {
        using Block<byte> data = Block<byte>.CreateUninitialized((int)(inStream.Length - inKeySize));
        inStream.ReadExactly(data);

        // TODO:
        // This is not how the game actually decrypts the data, but also seems to work
        // For actual decryption algorithm, see https://github.com/CadeEvs/FrostyToolsuite/blob/1.0.6.2/FrostySdk/Deobfuscators/MEADeobfuscator.cs
        Span<byte> bytes = data;
        byte initialValue = bytes[0];
        byte key = bytes[0];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte nextKey = (byte)((initialValue ^ bytes[i]) - (i % 256));
            bytes[i] ^= key;
            key = nextKey;
        }

        Span<byte> header = data.ToSpan();
        data.Shift(0x22C);

        using Stream dataStream = data.ToStream();
        return Deobfuscate(header, dataStream);
    }

    private static BlockAccessor? Deobfuscate(Stream inStream)
    {
        Span<byte> header = stackalloc byte[0x22C];
        inStream.ReadExactly(header);

        return Deobfuscate(header, inStream);
    }

    /// <returns>The deobfuscated data, or <see langword="null" /> if <paramref name="inHeader" /> is not obfuscation header.</returns>
    private static BlockAccessor? Deobfuscate(ReadOnlySpan<byte> inHeader, Stream inStream)
    {
        if (!(inHeader[0] == 0x00 && inHeader[1] == 0xD1 && inHeader[2] == 0xCE &&
              (inHeader[3] == 0x00 || inHeader[3] == 0x01 || inHeader[3] == 0x03))) // Version 0 is not used in FB3
        {
            return null;
        }

        BlockAccessor accessor = new(Block<byte>.CreateUninitialized((int)(inStream.Length - 0x22C)));
        inStream.ReadExactly(accessor._block);

        // Deobfuscate the data
        if (ProfilesLibrary.FrostbiteVersion > "2014.4.11" || inHeader[3] == 0x03)
        {
            return accessor;
        }

        Span<byte> bytes = accessor._block;
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= (byte)(0x7B ^ inHeader[0x128 + (i % 0x101)]);
        }

        return accessor;
    }

    private void TrimBlockToLength()
    {
        if (Position > Length)
        {
            Span<byte> padding = new byte[Position - Length];
            Position = Length;
            BaseStream.Write(padding);
        }

        if (_block.Size != Length)
        {
            // Resize the block if needed
            _block.Resize((int)Length);
        }
    }

    private void ResizeStream(long inDesiredMinLength)
    {
        if (inDesiredMinLength <= _block.Size)
        {
            return;
        }

        long position = Position;
        int neededLength = (int)Math.Max(inDesiredMinLength, Environment.SystemPageSize + position);
        neededLength = (neededLength + 15) & ~15;
        _block.Resize(neededLength);

        BaseStream = _block.ToStream();
        BaseStream.SetLength(inDesiredMinLength);
        BaseStream.Position = position;
    }
}