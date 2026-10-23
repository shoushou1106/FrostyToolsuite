using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Frosty.Sdk.Utils;

/// <summary>
///     Manages a sequential area of memory like <see cref="Span{T}" /> on the managed heap.
/// </summary>
/// <typeparam name="T">Underlying type.</typeparam>
public sealed class Block<T> : IDisposable where T : unmanaged
{
    private int _baseByteOffset;

    private byte[] _buffer;
    private bool _disposed;

    /// <summary>
    ///     Allocates a new zeroed block of memory.
    /// </summary>
    /// <param name="inSize">The total number of items to allocate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inSize" /> is invalid.</exception>
    /// <seealso cref="CreateUninitialized" />
    public Block(int inSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inSize);

        _buffer = new byte[inSize * ElementSize];
        BaseSize = inSize;
    }

    /// <summary>
    ///     Creates a <see cref="Block{T}" /> view around a shifted existing <see cref="Block{T}" />.
    /// </summary>
    /// <param name="inBlock">The source block.</param>
    /// <param name="inSize">The number of elements (not bytes!) in the region.</param>
    /// <exception cref="ObjectDisposedException"><paramref name="inBlock" /> is disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="inBlock" /> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inSize" /> is invalid.</exception>
    public Block(Block<T> inBlock, int inSize)
    {
        ArgumentNullException.ThrowIfNull(inBlock);

        ObjectDisposedException.ThrowIf(inBlock._disposed, inBlock);
        ArgumentOutOfRangeException.ThrowIfNegative(inSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inSize, inBlock.Size);

        _buffer = inBlock._buffer;
        _baseByteOffset = inBlock.CurrentByteOffset;
        BaseSize = inSize;
    }

    /// <summary>
    ///     Creates a <see cref="Block{T}" /> view around a region of an existing <see cref="Block{T}" /> relative to its base.
    /// </summary>
    /// <param name="inBlock">The source block.</param>
    /// <param name="inStartOffset">The element offset from the base of <paramref name="inBlock" />.</param>
    /// <param name="inSize">The number of elements (not bytes!) in the region.</param>
    /// <exception cref="ObjectDisposedException"><paramref name="inBlock" /> is disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="inBlock" /> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inSize" /> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inStartOffset" /> is invalid.</exception>
    public Block(Block<T> inBlock, int inStartOffset, int inSize)
    {
        ArgumentNullException.ThrowIfNull(inBlock);

        ObjectDisposedException.ThrowIf(inBlock._disposed, inBlock);
        ArgumentOutOfRangeException.ThrowIfNegative(inStartOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(inSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inStartOffset + inSize, inBlock.BaseSize);

        _buffer = inBlock._buffer;
        _baseByteOffset = inBlock._baseByteOffset + (inStartOffset * ElementSize);
        BaseSize = inSize;
    }

    /// <summary>
    ///     Creates a <see cref="Block{T}" /> from a <see cref="ReadOnlySpan{T}" /> by copying its data.
    /// </summary>
    /// <param name="inSpan">Source data.</param>
    public Block(ReadOnlySpan<T> inSpan)
    {
        // Copy as byte[] instead of the original pointer.
        // Because "fixed" only protects the memory when inside {}.
        // https://learn.microsoft.com/dotnet/standard/unsafe-code/best-practices#2-exposing-pointers-outside-of-the-fixed-scope
        // fixed (T* ptr = inSpan)
        // {
        //     // Pointer was saved and lived outside
        //     BasePtr = ptr;
        //     Ptr = BasePtr;
        //     BaseSize = inSpan.Length;
        //     m_fragileMemory = true;
        // }

        _buffer = [.. MemoryMarshal.AsBytes(inSpan)];
        BaseSize = inSpan.Length;
    }

    /// <summary>
    ///     Creates a <see cref="Block{T}" /> around a <typeparamref name="T" />[] and copies the data.
    /// </summary>
    /// <param name="inArray">Source data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inArray" /> is invalid.</exception>
    public Block(T[] inArray)
    {
        ArgumentNullException.ThrowIfNull(inArray);

        _buffer = [.. MemoryMarshal.AsBytes(inArray.AsSpan())];
        BaseSize = inArray.Length;
    }

    /// <summary>
    ///     Privately used in ToBlock and CreateUninitialized
    /// </summary>
    private Block(byte[] inBuffer, int inByteOffset, int inSize)
    {
        _buffer = inBuffer;
        _baseByteOffset = inByteOffset;
        BaseSize = inSize;
    }

    /// <remarks>
    ///     Unsafe.SizeOf is safe.
    /// </remarks>
    private static int ElementSize => Unsafe.SizeOf<T>();

    /// <summary>
    ///     Absolute size of the block in elements.
    /// </summary>
    public int BaseSize { get; private set; }

    /// <summary>
    ///     Shifted offset of the block in elements.
    ///     <see cref="BaseSize" /> - <see cref="ShiftAmount" /> gets the current <see cref="Size" />.
    /// </summary>
    public int ShiftAmount { get; private set; }

    public int Size => BaseSize - ShiftAmount;

    private int CurrentByteOffset => _baseByteOffset + (ShiftAmount * ElementSize);

    /// <summary>
    ///     Attempts to set or get the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if the index is negative or larger than the size of this <see cref="Block{T}" />.
    /// </exception>
    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Size);
            return ToReadOnlySpan()[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Size);
            ToSpan()[index] = value;
        }
    }

    /// <summary>
    ///     Dispose the block and drops its reference to the memory, the memory stays alive only as long as views of it do.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _buffer = [];
        _baseByteOffset = 0;
    }

    /// <summary>
    ///     Allocates a new block of memory without zeroing it, so it holds leftover data until written.
    ///     Only use it when the whole block is overwritten right away, like reading a file into it.
    /// </summary>
    /// <param name="inSize">The total number of items to allocate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inSize" /> is invalid.</exception>
    // CA1000: Do not declare static members on generic types
#pragma warning disable CA1000
    public static Block<T> CreateUninitialized(int inSize)
#pragma warning restore CA1000
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inSize);

        return new Block<T>(GC.AllocateUninitializedArray<byte>(inSize * ElementSize), 0, inSize);
    }

    /// <summary>
    ///     Clears the whole Block.
    /// </summary>
    public void Clear()
    {
        BaseBytes().Clear();
    }

    /// <summary>
    ///     Resizes the block to the specified size.
    ///     The block gets its own memory, elements added to grow the block are zero.
    /// </summary>
    /// <param name="inNewSize">The new size of the block.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inNewSize" /> must be at least 1.</exception>
    public void Resize(int inNewSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(inNewSize, 1);

        Span<byte> oldBytes = BaseBytes();
        // ReAlloc may move the data
        byte[] newBuffer = GC.AllocateUninitializedArray<byte>(inNewSize * ElementSize);

        int copied = Math.Min(oldBytes.Length, newBuffer.Length);
        oldBytes[..copied].CopyTo(newBuffer);
        newBuffer.AsSpan(copied).Clear();

        _buffer = newBuffer;
        _baseByteOffset = 0;
        BaseSize = inNewSize;
    }

    /// <summary>
    ///     Shifts the beginning of the Block by the specified amount.
    /// </summary>
    /// <param name="inAmount">
    ///     Number of elements to shift the <see cref="Block{T}" /> start by.
    ///     Can be negative, but attempting to shift before the beginning of the block will result in an exception.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if attempting to shift before the beginning of the block or after the end of the block.
    /// </exception>
    public void Shift(int inAmount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ShiftAmount + inAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inAmount),
                "Attempted to shift before the beginning of the block!");
        }

        if (inAmount > Size)
        {
            throw new ArgumentOutOfRangeException(nameof(inAmount), "Attempted to shift after the end of the block!");
        }

        // Instead of playing with two pointers, save the shift amount instead.
        ShiftAmount += inAmount;
    }

    /// <summary>
    ///     Resets the Shift back to 0.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Block disposed.</exception>
    public void ResetShift()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ShiftAmount = 0;
    }

    public T[] ToArray()
    {
        return [.. ToReadOnlySpan()];
    }

    public Span<T> ToSpan()
    {
        return ToSpan(0, Size);
    }

    public ReadOnlySpan<T> ToReadOnlySpan()
    {
        return ToSpan(0, Size);
    }

    public ReadOnlySpan<T> ToReadOnlySpan(int inStart)
    {
        return ToSpan(inStart);
    }

    public ReadOnlySpan<T> ToReadOnlySpan(int inStart, int inLength)
    {
        return ToSpan(inStart, inLength);
    }

    /// <summary>
    ///     Reinterprets the current window as a <see cref="Block{TOther}" /> sharing the same memory.
    /// </summary>
    public Block<TOther> ToBlock<TOther>() where TOther : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The original forgot to mark it as fragile. The new one was not fragile.
        // Unsafe.SizeOf is safe.
        return new Block<TOther>(_buffer, CurrentByteOffset, Size * ElementSize / Unsafe.SizeOf<TOther>());
    }

    /// <summary>
    ///     Gets a span starting <paramref name="inStart" /> elements after the shifted start until the end of the block.
    ///     <paramref name="inStart" /> may be negative to reach back the shifted part.
    /// </summary>
    public Span<T> ToSpan(int inStart)
    {
        return ToSpan(inStart, Size - inStart);
    }

    /// <summary>
    ///     Gets a span starting <paramref name="inStart" /> elements after the shifted start.
    ///     <paramref name="inStart" /> may be negative to reach back the shifted part.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Block disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inStart" /> is out of range.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inLength" /> is invalid.</exception>
    public Span<T> ToSpan(int inStart, int inLength)
    {
        // Keep all the checks here
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ShiftAmount + inStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inStart),
                "Attempted to slice before the beginning of the block!");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(inLength);
        if (inStart + inLength > Size)
        {
            throw new ArgumentOutOfRangeException(nameof(inLength), "Attempted to slice after the end of the block!");
        }

        return MemoryMarshal.Cast<byte, T>(
            _buffer.AsSpan(CurrentByteOffset + (inStart * ElementSize), inLength * ElementSize));
    }

    // CA1002: Do not expose generic lists
#pragma warning disable CA1002
    public List<T> ToList()
#pragma warning restore CA1002
    {
        return [.. ToReadOnlySpan()];
    }

    /// <summary>
    ///     Creates a fixed capacity stream over the whole block, positioned at the shifted start.
    ///     Writes through the stream change the block.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Block disposed.</exception>
    public Stream ToStream()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new MemoryStream(_buffer, _baseByteOffset, BaseSize * ElementSize, true, true)
        {
            Position = ShiftAmount * ElementSize
        };
    }

    /// <summary>
    ///     Interprets the block as a UTF-8 null-terminated string.
    /// </summary>
    /// <returns>A string instance from the data of the block.</returns>
    public static explicit operator string(Block<T> inBlock)
    {
        ArgumentNullException.ThrowIfNull(inBlock);

        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(inBlock.ToReadOnlySpan());
        int nullIndex = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(nullIndex >= 0 ? bytes[..nullIndex] : bytes);
    }

    public static implicit operator Span<T>(Block<T>? inBlock)
    {
        return inBlock is null ? [] : inBlock.ToSpan();
    }

    public static implicit operator ReadOnlySpan<T>(Block<T>? inBlock)
    {
        return inBlock is null ? [] : inBlock.ToReadOnlySpan();
    }

    /// <summary>
    ///     Copies the data of this <see cref="Block{T}" /> to the specified destination.
    /// </summary>
    /// <param name="inDest">The block to copy to.</param>
    public void CopyTo(Block<T> inDest)
    {
        ArgumentNullException.ThrowIfNull(inDest);

        if (inDest.BaseSize < BaseSize)
        {
            throw new ArgumentOutOfRangeException(nameof(inDest),
                "The target block must be at least as big as the source block!");
        }

        BaseBytes().CopyTo(inDest.BaseBytes());
    }

    /// <summary>
    ///     Copies part of the data of this <see cref="Block{T}" /> to the specified destination.
    /// </summary>
    /// <param name="inDest">The block to copy to.</param>
    /// <param name="inSize">The number of elements (not bytes!) to copy.</param>
    public void CopyTo(Block<T> inDest, int inSize)
    {
        ArgumentNullException.ThrowIfNull(inDest);

        if (inSize > Size)
        {
            throw new ArgumentOutOfRangeException(nameof(inSize), "The source block is not big enough!");
        }

        if (inDest.Size < inSize)
        {
            throw new ArgumentOutOfRangeException(nameof(inDest), "The target block is not big enough!");
        }

        ToReadOnlySpan(0, inSize).CopyTo(inDest.ToSpan(0, inSize));
    }

    public override string ToString()
    {
        return $"Block<{typeof(T).Name}>[{Size}]";
    }

    /// <exception cref="ObjectDisposedException">Block disposed.</exception>
    private Span<byte> BaseBytes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer.AsSpan(_baseByteOffset, BaseSize * ElementSize);
    }
}