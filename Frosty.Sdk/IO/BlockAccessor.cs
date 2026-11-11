using System.Security.Cryptography;
using System.Text;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

/// <summary>
///     A <see cref="DataAccessor" /> over a <see cref="Block{T}" /> in memory, which grows when written past its end.
/// </summary>
// Temporary, lists the callers left until it is deleted.
[Obsolete("BlockAccessor will be removed. Use a DataAccessor over a MemoryStream instead.",
    DiagnosticId = "FrostyTempBlockAccessorIsDeprecated")]
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

    public override void CopyTo(DataAccessor destination, int count)
    {
        if (destination is not BlockAccessor accessor)
        {
            base.CopyTo(destination, count);
            return;
        }

        if (count <= 0)
        {
            return;
        }

        accessor.ResizeStream(accessor.Position + count);

        if (accessor.Length < accessor.Position + count)
        {
            accessor.BaseStream.SetLength((int)accessor.Position + count);
        }

        using Block<byte> source = new(_block, (int)Position, count);
        using Block<byte> dest = new(accessor._block, (int)accessor.Position, count);
        source.CopyTo(dest);

        accessor.Position += count;

        Position += count;
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
    ///     Loads the whole file into memory and deobfuscates it if necessary.
    /// </summary>
    /// <param name="inPath">The path of the file</param>
    /// <param name="inShouldDeobfuscate">Whether the file can have an obfuscation header, see <see cref="ObfuscatedFile" />.</param>
    /// <returns>A <see cref="BlockAccessor" /> that has the file loaded.</returns>
    public static BlockAccessor FromFile(string inPath, bool inShouldDeobfuscate)
    {
        byte[] data = inShouldDeobfuscate ? ObfuscatedFile.ReadAllBytes(inPath) : File.ReadAllBytes(inPath);
        return new BlockAccessor(Block<byte>.Wrap(data));
    }

    /// <summary>
    ///     Loads part of a file into memory.
    /// </summary>
    /// <param name="inPath">The path of the file.</param>
    /// <param name="inOffset">The offset of the data to load.</param>
    /// <param name="inSize">The size of the data to load</param>
    /// <returns>A <see cref="BlockAccessor" /> that has the data loaded.</returns>
    public static BlockAccessor FromFile(string inPath, long inOffset, int inSize)
    {
        using FileStream stream = new(inPath, FileMode.Open, FileAccess.Read);
        stream.Position = inOffset;

        BlockAccessor retVal = new(Block<byte>.CreateUninitialized(inSize));
        stream.ReadExactly(retVal._block);
        return retVal;
    }

    /// <summary>
    ///     <see cref="Aes" /> decrypt this <see cref="BlockAccessor" />.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode" /> to use for the decryption.</param>
    public void Decrypt(byte[] inKey, PaddingMode inPaddingMode)
    {
        using Aes aes = Aes.Create();
        aes.Key = inKey;
        // The block can be larger than what was written, so stop at Length
        Span<byte> span = _block.ToSpan((int)Position, (int)(Length - Position));
        aes.DecryptCbc(span, inKey, span, inPaddingMode);
    }

    /// <summary>
    ///     <see cref="Aes" /> decrypt part of this <see cref="BlockAccessor" />.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inSize">The size of the data to decrypt.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode" /> to use for the decryption.</param>
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
        // turns writing a large file into quadratic copying.
        long newLength = Math.Max(inDesiredMinLength,
            Math.Max(_block.Size * 2L, Environment.SystemPageSize + position));
        newLength = Math.Min((newLength + 15) & ~15, Array.MaxLength);
        if (newLength < inDesiredMinLength)
        {
            throw new IOException($"A {nameof(BlockAccessor)} cannot grow beyond {Array.MaxLength} bytes.");
        }

        _block.Resize((int)newLength);

        BaseStream = _block.ToStream();
        BaseStream.SetLength(inDesiredMinLength);
        BaseStream.Position = position;
    }
}