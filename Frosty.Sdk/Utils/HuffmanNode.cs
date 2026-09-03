using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

/// <summary>
/// A node in the huffman coding scheme
/// </summary>
internal class HuffmanNode : IComparable<HuffmanNode>
{
    public bool IsLeaf => Left == null && Right == null;
    public char Letter => (char)(~Value);

    public uint Value;
    public HuffmanNode? Left { get; private set; }
    public HuffmanNode? Right { get; private set; }

    public HuffmanNode? Parent { get; private set; }

    public HuffmanNode()
    {
    }

    public HuffmanNode(uint inValue, HuffmanNode inLeft, HuffmanNode inRight)
    {
        Value = inValue;
        SetLeftNode(inLeft);
        SetRightNode(inRight);
    }

    public HuffmanNode(DataStream stream, Endian endian)
    {
        Value = stream.ReadUInt32(endian);
    }

    public void SetLeftNode(HuffmanNode leftNode)
    {
        Left = leftNode;
        Left.Parent = this;
    }

    public void SetRightNode(HuffmanNode rightNode)
    {
        Right = rightNode;
        Right.Parent = this;
    }

    public override string ToString()
    {
        string printLetter = Value switch
        {
            uint.MaxValue => "endDelimiter",
            4294967285 => "newLine",
            _ => Letter.ToString(),
        };
        return $"[Value = <{Value}> | Letter = <{printLetter}>]";
    }

    /// <summary>
    /// Returns the bit representation of this node, to be used in tests.
    /// </summary>
    /// <returns>The bit representation of this node.</returns>
    public string GetBitRepresentation()
    {
        if (Parent == null)
        {
            return "";
        }

        string bitVal;
        if (this == Parent.Left)
        {
            bitVal = "0";
        }
        else if (this == Parent.Right)
        {
            bitVal = "1";
        }
        else
        {
            bitVal = "ERROR!";
        }
        return Parent.GetBitRepresentation() + bitVal;
    }

    public int CompareTo(HuffmanNode? other)
    {
        return Value.CompareTo(other?.Value);
    }
}