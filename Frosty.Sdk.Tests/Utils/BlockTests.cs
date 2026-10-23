using System.Runtime.InteropServices;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.Utils;

// Assertion delegates run synchronously before disposing captured blocks.

// CA1861: Avoid constant arrays as arguments
#pragma disable CA1861
// Code inspection: Access to disposed captured variable
// ReSharper disable AccessToDisposedClosure
/// <summary>
///     Tests for <see cref="Frosty.Sdk.Utils.Block{T}" /> class to make sure it behaves as the old version.
/// </summary>
internal sealed class BlockTests
{
    /// <summary>
    ///     Create sequence looks like this: [0, 1, 2, 3, ..., inSize - 1]
    /// </summary>
    private static Block<int> CreateSequence(int inSize)
    {
        Block<int> block = new(inSize);
        for (int i = 0; i < inSize; i++)
        {
            block[i] = i;
        }

        return block;
    }

    /// <summary>
    ///     Verify that views created from a block share memory with each other.
    /// </summary>
    [Test]
    public void Views_ShareMemoryWithSource()
    {
        // [0, 1, 2, 3, 4, 5, 6, 7]
        using Block<int> source = CreateSequence(8);
        // 0, 1, [2, 3, 4, 5, 6, 7]
        source.Shift(2);

        // 0, 1, [2, 3, 4], 5, 6, 7
        using Block<int> fromShift = new(source, 3);
        // 0, 1, 2, 3, 4, 5, [6, 7]
        using Block<int> fromBase = new(source, 6, 2);

        // ReSharper disable once GrammarMistakeInComment
        // 0, 1,  2, 3, 4, 5, 6,  7
        // Modify item 2 to -1
        fromShift[0] = -1;
        // Modify item 7 to -2
        fromBase[1] = -2;
        // 0, 1, -1, 3, 4, 5, 6, -2

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromShift.ToArray(), Is.EqualTo<int[]>([-1, 3, 4]));
            Assert.That(source.ToArray(), Is.EqualTo<int[]>([-1, 3, 4, 5, 6, -2]));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Block<int>(source, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Block<int>(source, 7, 2));
        }
    }

    /// <summary>
    ///     Verify that shifting a block moves the window within bounds. Throws exceptions when out of bounds.
    /// </summary>
    [Test]
    public void Shift_MovesWindowWithinBounds()
    {
        using Block<int> block = CreateSequence(4);

        // Shift the block and test
        block.Shift(3);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(block.Size, Is.EqualTo(1));
            Assert.That(block[0], Is.EqualTo(3));
            Assert.That(block.ToReadOnlySpan(-2).ToArray(), Is.EqualTo<int[]>([1, 2, 3]));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.Shift(-4));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.Shift(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = block[1]);
        }

        // Reset the block and test
        block.ResetShift();
        Assert.That(block.Size, Is.EqualTo(4));
    }

    /// <summary>
    ///     Verify that ToBlock{TOther} reinterprets a shifted window in place.
    /// </summary>
    [Test]
    public void ToBlock_ReinterpretsShiftedWindowInPlace()
    {
        using Block<int> block = CreateSequence(3);
        block[1] = 0x01020304;
        block.Shift(1);

        using Block<byte> bytes = block.ToBlock<byte>();
        Assert.That(bytes.ToArray(), Is.EqualTo<byte[]>([.. MemoryMarshal.AsBytes(block.ToReadOnlySpan())]));

        bytes.Clear();
        Assert.That(block.ToArray(), Is.EqualTo<int[]>([0, 0]));
    }

    /// <summary>
    ///     Verify that resizing a block keeps data, zeroes growth, and create new memory.
    /// </summary>
    [Test]
    public void Resize_KeepsDataZeroesGrowthAndDetachesViews()
    {
        // 0, 1, 2, 3
        using Block<int> block = CreateSequence(4);
        using Block<int> view = new(block, 4);

        block.Resize(6);
        view[0] = -1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(block.ToArray(), Is.EqualTo<int[]>([0, 1, 2, 3, 0, 0]));
            Assert.That(view[0], Is.EqualTo(-1));
        }

        block.Resize(2);
        Assert.That(block.ToArray(), Is.EqualTo<int[]>([0, 1]));
    }

    /// <summary>
    ///     Verify that ToStream returns a stream that spans the whole block, support shifting.
    /// </summary>
    [Test]
    public void ToStream_SpansWholeBlockFromShiftWithFixedCapacity()
    {
        using Block<ushort> block = new(4);
        block.Clear();
        block.Shift(1);

        using Stream stream = block.ToStream();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream.Length, Is.EqualTo(8));
            Assert.That(stream.Position, Is.EqualTo(2));
        }

        stream.Write([0xFF, 0xFF]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(block[0], Is.EqualTo(ushort.MaxValue));
            Assert.Throws<NotSupportedException>(() => stream.Write(new byte[7]));
        }
    }

    [TestCase(new byte[] { 0x61, 0x62, 0x63, 0x00, 0x64 }, "abc")]
    [TestCase(new byte[] { 0x61, 0x62, 0x63 }, "abc")]
    public void ExplicitString_StopsAtTerminatorOrBlockEnd(byte[] inData, string inExpected)
    {
        using Block<byte> block = new(inData);
        Assert.That((string)block, Is.EqualTo(inExpected));
    }

    [Test]
    public void CopyTo_CopiesFromShiftedWindow()
    {
        using Block<int> source = CreateSequence(5);
        using Block<int> dest = new(3);
        dest.Clear();
        source.Shift(2);

        source.CopyTo(dest, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dest.ToArray(), Is.EqualTo<int[]>([2, 3, 0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.CopyTo(dest, 4));
        }
    }

    /// <summary>
    ///     Verify that disposing block does not invalidate views created from it (shared memory).
    /// </summary>
    [Test]
    public void Dispose_LeavesViewsUsable()
    {
        Block<int> block = CreateSequence(3);
        using Block<int> view = new(block, 3);

        block.Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ObjectDisposedException>(() => _ = block[0]);
            Assert.Throws<ObjectDisposedException>(() => block.ToStream());
            Assert.That(view.ToArray(), Is.EqualTo<int[]>([0, 1, 2]));
        }
    }
}