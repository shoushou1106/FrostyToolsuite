namespace Frosty.Sdk.Utils.Huffman;

/// <summary>
///     A <see cref="HuffmanNode" /> that also counts how often its symbols occur, used to build the Huffman tree.
/// </summary>
internal sealed class HuffmanConstructionNode : HuffmanNode, IComparable<HuffmanConstructionNode>
{
    // The number of levels below this node, 0 for a leaf
    private int _depth;

    // How often the symbols below this node occur in total
    private int _occurrences;

    public HuffmanConstructionNode()
    {
    }

    public HuffmanConstructionNode(char valueChar, int occurrences)
    {
        Value = ~(uint)valueChar;
        _occurrences = occurrences;
    }

    public int CompareTo(HuffmanConstructionNode? other)
    {
        if (other is null)
        {
            return 1;
        }

        int cmp = _occurrences.CompareTo(other._occurrences);
        return cmp != 0 ? cmp : _depth.CompareTo(other._depth);
    }

    public void SetLeftNode(HuffmanConstructionNode leftNode)
    {
        base.SetLeftNode(leftNode);
        AddChild(leftNode);
    }

    public void SetRightNode(HuffmanConstructionNode rightNode)
    {
        base.SetRightNode(rightNode);
        AddChild(rightNode);
    }

    private void AddChild(HuffmanConstructionNode child)
    {
        _occurrences += child._occurrences;
        _depth = Math.Max(_depth, child._depth + 1);
    }
}
