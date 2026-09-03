namespace Frosty.Sdk.Utils;

/// <summary>
/// <list type="bullet">
/// <item>
/// <description><c>EncodingResult.EncodingTree</c> returns the huffman tree used for encoding in the form of an integer list.</description>
/// </item>
/// <item>
/// <description><c>EncodingResult.EncodedTexts</c> returns the given strings encoded to a byte array..</description>
/// </item>
/// <item>
/// <description><c>EncodingResult.GetTextPositionsDictionary()</c> returns a dictionary with the given strings as keys and their bit offsets in the <c>EncodedTexts</c>.</description>
/// </item>
/// <item>
/// <description><c>EncodingResult.EncodedTextPositions</c> returns the same data as above in the form of a list of tuples for use in iterations.</description>
/// </item>
/// </list>
/// </summary>
public class EncodingResult : HuffmanEncodedTextArray<string>
{
    /// <summary>
    /// The encoding tree in the form of an integer list.
    /// </summary>
    public IList<uint> EncodingTree { get; private set; }

    /// <summary>
    /// Creates a new result object from an existing HuffmanEncodedTextArray with additional encoding tree information.
    /// </summary>
    /// <param name="inEncodedTextArray"> the original result</param>
    /// <param name="inEncodingTree"> the encoding tree</param>
    public EncodingResult(HuffmanEncodedTextArray<string> inEncodedTextArray, IList<uint> inEncodingTree)
        : base(inEncodedTextArray)
    {
        EncodingTree = inEncodingTree;
    }
}