using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads and writes Frostbite data on top of a <see cref="Stream" />: numbers in either <see cref="Endian" />,
///     strings (UTF-8 unless an encoding is given), 7 bit encoded integers, guids and sha1s.
/// </summary>
public class DataStream : IDisposable
{
    private readonly Stack<long> _steps = new();
    private readonly bool _leaveOpen;
    private Block<byte>? _stringBuffer;
    private bool _disposed;

    protected DataStream()
    {
        BaseStream = Stream.Null;
    }

    /// <param name="inStream">The stream to read from and write to.</param>
    /// <param name="inLeaveOpen">
    ///     <see langword="true" /> to leave <paramref name="inStream" /> open when this <see cref="DataStream" /> is disposed.
    /// </param>
    public DataStream(Stream inStream, bool inLeaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inStream);

        BaseStream = inStream;
        _leaveOpen = inLeaveOpen;
    }

    /// <summary>
    ///     The stream this <see cref="DataStream" /> reads from and writes to.
    /// </summary>
    public Stream BaseStream { get; protected set; }

    /// <inheritdoc cref="Stream.Position" />
    public long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    /// <inheritdoc cref="Stream.Length" />
    public long Length => BaseStream.Length;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc cref="Stream.Seek" />
    public long Seek(long offset, SeekOrigin origin)
    {
        return BaseStream.Seek(offset, origin);
    }

    /// <inheritdoc cref="Stream.CopyTo(Stream)" />
    public void CopyTo(Stream destination)
    {
        CopyTo(destination, (int)(Length - Position));
    }

    /// <inheritdoc cref="Stream.CopyTo(Stream, int)" />
    public virtual void CopyTo(Stream destination, int bufferSize)
    {
        BaseStream.CopyTo(destination, bufferSize);
    }

    public void CopyTo(DataStream destination)
    {
        CopyTo(destination, (int)(Length - Position));
    }

    public virtual void CopyTo(DataStream destination, int bufferSize)
    {
        ArgumentNullException.ThrowIfNull(destination);

        CopyTo(destination.BaseStream, bufferSize);
    }

    /// <inheritdoc cref="Stream.SetLength" />
    public void SetLength(int value)
    {
        BaseStream.SetLength(value);
    }

    public void Pad(int alignment)
    {
        if (BaseStream.Position % alignment != 0)
        {
            BaseStream.Position += alignment - (BaseStream.Position % alignment);
        }
    }

    public void StepIn(long inPosition)
    {
        _steps.Push(Position);
        Position = inPosition;
    }

    public void StepOut()
    {
        Debug.Assert(_steps.Count > 0, "StepOut called when there were no steps taken.");
        Position = _steps.Pop();
    }

    public virtual DataStream CreateSubStream(long inStartOffset, int inSize)
    {
        StepIn(inStartOffset);

        DataStream retVal = new(new MemoryStream(ReadBytes(inSize)));

        StepOut();

        return retVal;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _disposed = true;

        if (!_leaveOpen)
        {
            BaseStream.Dispose();
        }

        _stringBuffer?.Dispose();
    }

    #region -- Read --

    /// <summary>
    ///     Reads count number of bytes from the current stream and advances the position within the stream.
    /// </summary>
    /// <param name="count">The number of bytes to be read from the current stream.</param>
    /// <returns>A byte array containing the read bytes.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached before reading count number of bytes.</exception>
    public byte[] ReadBytes(int count)
    {
        byte[] retVal = new byte[count];
        BaseStream.ReadExactly(retVal);
        return retVal;
    }

    /// <inheritdoc cref="Stream.ReadExactly(Span{byte})" />
    public void ReadExactly(Span<byte> buffer)
    {
        BaseStream.ReadExactly(buffer);
    }

    /// <inheritdoc cref="Stream.Read(byte[], int, int)" />
    public int Read(byte[] buffer, int offset, int count)
    {
        return BaseStream.Read(buffer, offset, count);
    }

    /// <inheritdoc cref="Stream.Read(Span{byte})" />
    public int Read(Span<byte> buffer)
    {
        return BaseStream.Read(buffer);
    }

    #region -- Basic Types --

    public byte ReadByte()
    {
        int retVal = BaseStream.ReadByte();
        return retVal == -1 ? throw new EndOfStreamException() : (byte)retVal;
    }

    public sbyte ReadSByte()
    {
        return (sbyte)ReadByte();
    }

    public bool ReadBoolean()
    {
        return ReadByte() != 0;
    }

    /// <summary>
    ///     Reads one byte as one <see cref="char" />.
    /// </summary>
    /// <remarks>
    ///     <b>Encoding:</b> Latin-1 (one byte per char)
    ///     <para>Only for Frostbite fields, do NOT use it for normal usage.</para>
    /// </remarks>
    /// <seealso cref="WriteLatin1Char"/>
    public char ReadLatin1Char()
    {
        return (char)ReadByte();
    }

    public short ReadInt16(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadInt16BigEndian(buffer)
            : BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    public ushort ReadUInt16(Endian endian = Endian.Little)
    {
        return (ushort)ReadInt16(endian);
    }

    public int ReadInt32(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadInt32BigEndian(buffer)
            : BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public uint ReadUInt32(Endian endian = Endian.Little)
    {
        return (uint)ReadInt32(endian);
    }

    public long ReadInt64(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public ulong ReadUInt64(Endian endian = Endian.Little)
    {
        return (ulong)ReadInt64(endian);
    }

    public Half ReadHalf(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Half>()];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadHalfBigEndian(buffer)
            : BinaryPrimitives.ReadHalfLittleEndian(buffer);
    }

    public float ReadSingle(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadSingleBigEndian(buffer)
            : BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public double ReadDouble(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BaseStream.ReadExactly(buffer);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadDoubleBigEndian(buffer)
            : BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    #endregion

    #region -- Strings --

    /// <summary>
    ///     Reads bytes up to the next null terminator and decodes them.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="ReadNullTerminatedString(Encoding)"/></remarks>
    /// <exception cref="EndOfStreamException">The stream ends before a null terminator.</exception>
    public string ReadNullTerminatedString() => ReadNullTerminatedString(Encoding.UTF8);

    /// <inheritdoc cref="ReadNullTerminatedString()" />
    public virtual string ReadNullTerminatedString(Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(inEncoding);

        _stringBuffer ??= new Block<byte>(Environment.SystemPageSize);

        int length = 0;
        while (true)
        {
            byte c = ReadByte();
            if (c == 0)
            {
                return inEncoding.GetString(_stringBuffer.ToReadOnlySpan(0, length));
            }

            if (length >= _stringBuffer.Size)
            {
                _stringBuffer.Resize(_stringBuffer.Size + Environment.SystemPageSize);
            }

            _stringBuffer[length++] = c;
        }
    }

    /// <summary>
    ///     Reads exactly <paramref name="size" /> bytes and decodes them without trailing <c>'\0'</c>.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="ReadFixedSizedString(int, Encoding)"/></remarks>
    public string ReadFixedSizedString(int size) => ReadFixedSizedString(size, Encoding.UTF8);

    /// <inheritdoc cref="ReadFixedSizedString(int)" />
    // ReSharper disable once MemberCanBePrivate.Global
    public string ReadFixedSizedString(int size, Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(inEncoding);

        using Block<byte> buffer = Block<byte>.CreateUninitialized(size);
        BaseStream.ReadExactly(buffer);
        return inEncoding.GetString(buffer).TrimEnd('\0');
    }

    /// <summary>
    ///     Reads a size in bytes as a 7-bit encoded int, and pass into <see cref="ReadFixedSizedString(int)" />.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="ReadSizedString(Encoding)"/></remarks>
    public string ReadSizedString() => ReadSizedString(Encoding.UTF8);

    /// <inheritdoc cref="ReadSizedString()" />
    // ReSharper disable once MemberCanBePrivate.Global
    public string ReadSizedString(Encoding inEncoding)
    {
        return ReadFixedSizedString(Read7BitEncodedInt32(), inEncoding);
    }

    #endregion

    public int Read7BitEncodedInt32()
    {
        uint result = 0;
        for (int shift = 0; shift < 28; shift += 7)
        {
            byte b = ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if (b <= 0x7F)
            {
                return (int)result;
            }
        }

        byte last = ReadByte();
        return last > 0b1111 ? throw new FormatException() : (int)(result | ((uint)last << 28));
    }

    public long Read7BitEncodedInt64()
    {
        ulong result = 0;
        for (int shift = 0; shift < 63; shift += 7)
        {
            byte b = ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            if (b <= 0x7F)
            {
                return (long)result;
            }
        }

        byte last = ReadByte();
        return last > 0b1 ? throw new FormatException() : (long)(result | ((ulong)last << 63));
    }

    public Guid ReadGuid(Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Guid>()];
        BaseStream.ReadExactly(buffer);

        return new Guid(buffer, endian == Endian.Big);
    }

    public Sha1 ReadSha1()
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Sha1>()];
        BaseStream.ReadExactly(buffer);

        return new Sha1(buffer);
    }

    #endregion

    #region -- Write --

    public virtual void Write(ReadOnlySpan<byte> buffer)
    {
        BaseStream.Write(buffer);
    }

    public virtual void Write(byte[] buffer, int offset, int count)
    {
        BaseStream.Write(buffer, offset, count);
    }

    #region -- Basic Types --

    public virtual void WriteByte(byte value)
    {
        BaseStream.WriteByte(value);
    }

    public void WriteSByte(sbyte value)
    {
        WriteByte((byte)value);
    }

    public void WriteBoolean(bool value)
    {
        WriteByte((byte)(value ? 1 : 0));
    }

    /// <summary>
    ///     Writes <paramref name="value" /> as one byte.
    /// </summary>
    /// <remarks>
    ///     <b>Encoding:</b> Latin-1 (one byte per char)
    ///     <para>Only for Frostbite fields, do NOT use it for normal usage.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> is above U+00FF.</exception>
    /// <seealso cref="ReadLatin1Char"/>
    public void WriteLatin1Char(char value)
    {
        // Fail fast, explicit exception for unwritable char
        // To avoid the English-only tragedy of v1
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, '\u00FF');

        WriteByte((byte)value);
    }

    public void WriteInt16(short value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        }

        Write(buffer);
    }

    public void WriteUInt16(ushort value, Endian endian = Endian.Little)
    {
        WriteInt16((short)value, endian);
    }

    public void WriteInt32(int value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        }

        Write(buffer);
    }

    public void WriteUInt32(uint value, Endian endian = Endian.Little)
    {
        WriteInt32((int)value, endian);
    }

    public void WriteInt64(long value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        }

        Write(buffer);
    }

    public void WriteUInt64(ulong value, Endian endian = Endian.Little)
    {
        WriteInt64((long)value, endian);
    }

    public void WriteHalf(Half value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Half>()];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteHalfBigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteHalfLittleEndian(buffer, value);
        }

        Write(buffer);
    }

    public void WriteSingle(float value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        }

        Write(buffer);
    }

    public void WriteDouble(double value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];

        if (endian == Endian.Big)
        {
            BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        }

        Write(buffer);
    }

    #endregion

    #region -- Strings --

    /// <summary>
    ///     Writes the encoded <paramref name="value" /> followed by one null byte.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="WriteNullTerminatedString(string, Encoding)"/></remarks>
    public void WriteNullTerminatedString(string value) => WriteNullTerminatedString(value, Encoding.UTF8);

    /// <inheritdoc cref="WriteNullTerminatedString(string)" />
    // ReSharper disable once MemberCanBePrivate.Global
    public void WriteNullTerminatedString(string value, Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(inEncoding);

        int size = inEncoding.GetByteCount(value) + 1;
        Span<byte> buffer = size <= 256 ? stackalloc byte[size] : new byte[size];

        inEncoding.GetBytes(value, buffer);
        buffer[^1] = 0;
        Write(buffer);
    }

    /// <summary>
    ///     Writes <paramref name="value" /> into exactly <paramref name="size" /> bytes, padded with zeros.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="WriteFixedSizedString(string, int, Encoding)"/></remarks>
    /// <exception cref="ArgumentException">The encoded string does not fit in <paramref name="size" /> bytes.</exception>
    public void WriteFixedSizedString(string value, int size) => WriteFixedSizedString(value, size, Encoding.UTF8);

    /// <inheritdoc cref="WriteFixedSizedString(string, int)" />
    // ReSharper disable once MemberCanBePrivate.Global
    public void WriteFixedSizedString(string value, int size, Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(inEncoding);

        Span<byte> buffer = size <= 256 ? stackalloc byte[size] : new byte[size];

        // Use Encoding.GetBytes instead of string.Length so it doesn't kill other languages
        // GetBytes throws the documented ArgumentException when the buffer is too small
        int written = inEncoding.GetBytes(value, buffer);
        // Span.Clear() fills 0 with good performance
        buffer[written..].Clear();
        Write(buffer);
    }

    /// <summary>
    ///     Writes the encoded size including a null terminator as a 7-bit encoded int, then the null-terminated string.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8, or any ASCII-compatible through <see cref="WriteSizedString(string, Encoding)"/></remarks>
    public void WriteSizedString(string value) => WriteSizedString(value, Encoding.UTF8);

    /// <inheritdoc cref="WriteSizedString(string)" />
    public void WriteSizedString(string value, Encoding inEncoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(inEncoding);

        int size = inEncoding.GetByteCount(value) + 1;
        Write7BitEncodedInt32(size);
        WriteFixedSizedString(value, size, inEncoding);
    }

    #endregion

    public void WriteGuid(Guid value, Endian endian = Endian.Little)
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Guid>()];

        value.TryWriteBytes(buffer, endian == Endian.Big, out _);

        Write(buffer);
    }

    public void WriteSha1(Sha1 value)
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Sha1>()];

        value.TryWriteBytes(buffer);

        Write(buffer);
    }

    public void Write7BitEncodedInt32(int value)
    {
        uint remaining = (uint)value;
        for (; remaining > 0x7F; remaining >>= 7)
        {
            WriteByte((byte)(remaining | 0x80));
        }

        WriteByte((byte)remaining);
    }

    public void Write7BitEncodedInt64(long value)
    {
        ulong remaining = (ulong)value;
        for (; remaining > 0x7F; remaining >>= 7)
        {
            WriteByte((byte)(remaining | 0x80));
        }

        WriteByte((byte)remaining);
    }

    #endregion
}
