using System.Buffers.Binary;
using System.Text;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

public class HuffmanDecoder
{
    private int[]? _data;

    /// <summary>
    ///     Root Node of the Huffman Table.
    /// </summary>
    private HuffmanNode? _rootNode;

    /// <summary>
    ///     Parses a Huffman Table.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream" /> the Huffman Table gets read from.</param>
    /// <param name="count">
    ///     The number of Huffman Nodes the Table contains.
    ///     This is the number of 32 bit Integers contained in the table or tree.
    /// </param>
    /// <param name="endian">The Endianess in which the Huffman Table gets read.</param>
    public void ReadHuffmanTable(DataStream stream, uint count, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _rootNode = null;
        HuffmanNode? leftNode = null;
        HuffmanNode? rightNode = null;

        List<HuffmanNode> nodes = [];
        uint nodeValue = 0;

        for (int i = 0; i < count; i++)
        {
            HuffmanNode n = new(stream, endian);
            int idx = nodes.FindIndex(a => a.Value == n.Value);
            if (idx != -1)
            {
                n = nodes[idx];
            }

            if (leftNode is null)
            {
                leftNode = n;
            }
            else if (rightNode is null)
            {
                rightNode = n;
                if (idx == -1)
                {
                    nodes.Add(rightNode);
                }

                n = new HuffmanNode(nodeValue++, leftNode, rightNode);
                _rootNode = n;

                leftNode = null;
                rightNode = null;
                idx = -1;
            }

            if (idx == -1)
            {
                nodes.Add(n);
            }
        }
    }

    /// <summary>
    ///     Reads in the encoded data.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream" /> the encoded data gets read from.</param>
    /// <param name="integerCount">The number of <see cref="int" />s the data contains.</param>
    /// <param name="endian">The <see cref="Endian" /> in which the encoded data gets read.</param>
    public void ReadEncodedData(DataStream stream, uint integerCount, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _data = new int[integerCount];

        for (int i = 0; i < integerCount; i++)
        {
            _data[i] = stream.ReadInt32(endian);
        }
    }

    /// <summary>
    ///     Reads in the encoded data of the given byte length, for cases that the data to read does not match integer sizes.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream" /> the encoded data gets read from.</param>
    /// <param name="byteCount">The number of <see cref="byte" />s to read.</param>
    /// <param name="endian">The <see cref="Endian" /> in which the encoded data gets read.</param>
    public void ReadOddSizedEncodedData(DataStream stream, uint byteCount, Endian endian = Endian.Little)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint intLength = byteCount / 4;

        _data = new int[intLength + 1];

        for (int i = 0; i < intLength; i++)
        {
            _data[i] = stream.ReadInt32(endian);
        }

        // Read remaining bytes as full int
        Span<byte> remaining = stackalloc byte[sizeof(int)];
        stream.ReadExactly(remaining[..((int)byteCount % 4)]);

        _data[intLength] = endian == Endian.Big
            ? BinaryPrimitives.ReadInt32BigEndian(remaining)
            : BinaryPrimitives.ReadInt32LittleEndian(remaining);
    }

    /// <summary>
    ///     Reads a Huffman encoded string.
    /// </summary>
    /// <param name="bitIndex">The index of the bit at which the encoded string starts.</param>
    /// <returns>The Huffman decoded string.</returns>
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
                node = bit == 0 ? node.Left! : node.Right!;
                bitIndex++;
            }

            if (node.Letter == 0x00
                // TODO: Should probably throw an exception instead or at least print a warning when the bit index is outside the read data array
                || bitIndex >= dataLengthInBits)
            {
                return sb.ToString();
            }

            sb.Append(node.Letter);
        }
    }

    public void Dispose()
    {
        _rootNode = null;
        _data = null;
    }
}