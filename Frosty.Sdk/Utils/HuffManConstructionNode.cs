namespace Frosty.Sdk.Utils;

/// <summary>
///     A <see cref="HuffmanNode" /> with additional integer for how many times this node was encountered in the data to
///     encode.
///     This is used to construct the huffman tree.
/// </summary>
internal sealed class HuffManConstructionNode : HuffmanNode, IComparable<HuffManConstructionNode>
{
    public HuffManConstructionNode()
    {
    }

    public HuffManConstructionNode(char inValueChar, int inOccurrences)
    {
        Value = ~(uint)inValueChar;
        Occurrences = inOccurrences;
    }

    public int Occurrences { get; private set; }

    public new HuffManConstructionNode? Left { get; private set; }

    public new HuffManConstructionNode? Right { get; private set; }

    public int CompareTo(HuffManConstructionNode? other)
    {
        if (other is null)
        {
            return 1;
        }

        int cmp = Occurrences.CompareTo(other.Occurrences);
        if (cmp == 0)
        {
            cmp = GetRemainingDepth().CompareTo(other.GetRemainingDepth());
        }

        return cmp;
    }

    public void SetLeftNode(HuffManConstructionNode leftNode)
    {
        base.SetLeftNode(leftNode);
        Left = leftNode;
        Occurrences += leftNode.Occurrences;
    }

    public void SetRightNode(HuffManConstructionNode rightNode)
    {
        base.SetRightNode(rightNode);
        Right = rightNode;
        Occurrences += rightNode.Occurrences;
    }

    private int GetRemainingDepth()
    {
        int ld = Left?.GetRemainingDepth() ?? 0;
        int rd = Right?.GetRemainingDepth() ?? 0;

        return Math.Max(ld, rd);
    }
}