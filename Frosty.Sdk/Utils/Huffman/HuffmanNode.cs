using System.Globalization;

namespace Frosty.Sdk.Utils.Huffman;

/// <summary>
///     A node of a Huffman tree.
/// </summary>
/// <remarks>
///     For a leaf node, <see cref="Value" /> is the bitwise NOT of its character (<c>~(uint)c</c>).
///     For a branch node, <see cref="Value" /> is its 0-based branch index in the tree.
/// </remarks>
internal class HuffmanNode : IComparable<HuffmanNode>
{
    protected HuffmanNode()
    {
    }

    public HuffmanNode(uint value)
    {
        Value = value;
    }

    public HuffmanNode(uint value, HuffmanNode left, HuffmanNode right)
    {
        Value = value;
        SetLeftNode(left);
        SetRightNode(right);
    }

    public bool IsLeaf => Left is null && Right is null;
    public char Letter => (char)~Value;

    public uint Value { get; init; }
    public HuffmanNode? Left { get; private set; }
    public HuffmanNode? Right { get; private set; }

    public HuffmanNode? Parent { get; private set; }

    public int CompareTo(HuffmanNode? other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    protected void SetLeftNode(HuffmanNode leftNode)
    {
        Left = leftNode;
        Left.Parent = this;
    }

    protected void SetRightNode(HuffmanNode rightNode)
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
            _ => Letter.ToString()
        };
        return string.Create(CultureInfo.InvariantCulture, $"[Value = <{Value}> | Letter = <{printLetter}>]");
    }
}