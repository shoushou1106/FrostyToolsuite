using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils.Huffman;

public sealed class HuffmanDecoder
{
    private int[]? _data;

    /// <summary>
    ///     Root Node of the Huffman Table.
    /// </summary>
    private HuffmanNode? _rootNode;

    /// <summary>
    ///     Parses a Huffman Table.
    /// </summary>
    /// <param name="accessor">The <see cref="DataAccessor" /> the Huffman Table gets read from.</param>
    /// <param name="count">
    ///     The number of Huffman Nodes the Table contains.
    ///     This is the number of 32-bit Integers contained in the table or tree.
    /// </param>
    /// <param name="endian">The endianness in which the Huffman Table gets read.</param>
    public void ReadHuffmanTable(DataAccessor accessor, uint count, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _rootNode = null;
        HuffmanNode? leftNode = null;

        Dictionary<uint, HuffmanNode> nodes = [];
        uint nodeValue = 0;

        for (int i = 0; i < count; i++)
        {
            uint value = accessor.ReadUInt32(endian);
            if (!nodes.TryGetValue(value, out HuffmanNode? node))
            {
                node = new HuffmanNode(value);
                nodes.Add(value, node);
            }

            if (leftNode is null)
            {
                // Node is left child
                leftNode = node;
                continue;
            }

            // Node is right child
            HuffmanNode branch = new(nodeValue++, leftNode, node);
            nodes.TryAdd(branch.Value, branch);
            _rootNode = branch;
            leftNode = null;
        }
    }

    /// <summary>
    ///     Reads in the encoded data.
    /// </summary>
    /// <param name="accessor">The <see cref="DataAccessor" /> the encoded data gets read from.</param>
    /// <param name="integerCount">The number of <see cref="int" />s the data contains.</param>
    /// <param name="endian">The <see cref="Endian" /> in which the encoded data gets read.</param>
    public void ReadEncodedData(DataAccessor accessor, uint integerCount, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _data = new int[integerCount];
        ReadIntegers(accessor, _data, endian);
    }

    /// <summary>
    ///     Reads in the encoded data of the given byte length, for cases that the data to read does not match integer sizes.
    /// </summary>
    /// <param name="accessor">The <see cref="DataAccessor" /> the encoded data gets read from.</param>
    /// <param name="byteCount">The number of <see cref="byte" />s to read.</param>
    /// <param name="endian">The <see cref="Endian" /> in which the encoded data gets read.</param>
    public void ReadOddSizedEncodedData(DataAccessor accessor, uint byteCount, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        uint intLength = byteCount / 4;

        _data = new int[intLength + 1];
        ReadIntegers(accessor, _data.AsSpan(0, (int)intLength), endian);

        // Read remaining bytes as full int
        Span<byte> remaining = stackalloc byte[sizeof(int)];
        accessor.ReadExactly(remaining[..((int)byteCount % 4)]);

        _data[intLength] = endian == Endian.Big
            ? BinaryPrimitives.ReadInt32BigEndian(remaining)
            : BinaryPrimitives.ReadInt32LittleEndian(remaining);
    }

    /// <summary>
    ///     Reads a Huffman encoded string.
    /// </summary>
    /// <param name="bitIndex">The index of the bit at which the encoded string starts.</param>
    /// <returns>The Huffman decoded string.</returns>
    /// <remarks><b>Encoding:</b> UTF-16, one char per Huffman leaf, any language in the table.</remarks>
    /// <exception cref="InvalidDataException">Is thrown when the root node or encoded data were not read in before.</exception>
    public string ReadHuffmanEncodedString(int bitIndex)
    {
        if (_rootNode is null || _data is null)
        {
            string rootNodeMissingErrorPart = _rootNode is not null
                ? string.Empty
                : " The root node was not established by calling 'ReadHuffmanTable'!";
            string dataMissingErrorPart = _data is not null
                ? string.Empty
                : " No huffman encoded data was read with 'ReadEncodedData'!";
            throw new InvalidDataException(
                $"HuffmanDecoder state is not initialized!{rootNodeMissingErrorPart}{dataMissingErrorPart}");
        }

        int dataLengthInBits = _data.Length * 32;

        StringBuilder sb = new();
        while (true)
        {
            HuffmanNode node = _rootNode;

            while (!node.IsLeaf && bitIndex < dataLengthInBits)
            {
                int bit = (_data[bitIndex / 32] >> (bitIndex % 32)) & 1;
                // Non-leaf nodes are always built with both children
                node = bit == 0 ? node.Left! : node.Right!;
                bitIndex++;
            }

            if (node.Letter == 0x00
                // TODO:
                // Should probably throw an exception instead
                // or at least print a warning
                // when the bit index is outside the read data array
                || bitIndex >= dataLengthInBits)
            {
                return sb.ToString();
            }

            sb.Append(node.Letter);
        }
    }

    private static void ReadIntegers(DataAccessor accessor, Span<int> destination, Endian endian)
    {
        // Read everything at once. The bytes were read in the host's endianness
        accessor.ReadExactly(MemoryMarshal.AsBytes(destination));
        if ((endian == Endian.Big) == BitConverter.IsLittleEndian)
        {
            // Big endian expected but current host use Little.
            BinaryPrimitives.ReverseEndianness(destination, destination);
        }
    }
}