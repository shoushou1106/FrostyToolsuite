namespace Frosty.Sdk.Utils.Huffman;

/// <summary>
///     The result of <see cref="HuffmanEncoder.Encode" />: the encoded texts plus the tree needed to decode them.
/// </summary>
/// <param name="encodedTextArray">The encoded texts and their bit positions.</param>
/// <param name="encodingTree">The node values of the Huffman tree, in the order they are written.</param>
public sealed class HuffmanEncodingResult(
    HuffmanEncodedTextArray<string> encodedTextArray,
    IReadOnlyList<uint> encodingTree)
    : HuffmanEncodedTextArray<string>(encodedTextArray)
{
    /// <summary>
    ///     The node values of the Huffman tree, in the order they are written.
    /// </summary>
    public IReadOnlyList<uint> EncodingTree { get; } = encodingTree;
}
