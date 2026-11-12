using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils.Huffman;

/// <summary>
///     Encodes strings with a Huffman tree built from them.
/// </summary>
/// <example>
///     Either call <see cref="Encode(IEnumerable{string}, Endian)" /> directly with the strings to encode,
///     or if more control is required, call <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> with a
///     representation of the strings to encode to build the encoding tree. After that, call
///     <see cref="EncodeTexts{T}" /> to get the encoding result.
/// </example>
public sealed class HuffmanEncoder
{
    private const char Delimiter = '\0';

    // The encoding of each char, as the path from the root (false is left)
    private Dictionary<char, List<bool>>? _characterEncoding;

    /// <summary>
    ///     Builds the Huffman tree for <paramref name="texts" /> and encodes them with it in one go.
    /// </summary>
    /// <remarks>Duplicate texts are encoded once.</remarks>
    /// <param name="texts">The texts to encode.</param>
    /// <param name="endian">The endianness to use for the encoded texts.</param>
    /// <returns>The encoded texts, their bit positions and the tree.</returns>
    public static HuffmanEncodingResult Encode(IEnumerable<string> texts, Endian endian = Endian.Little)
    {
        HashSet<string> uniqueTexts = [.. texts];

        HuffmanEncoder encoder = new();
        IReadOnlyList<uint> tree = encoder.BuildHuffmanEncodingTree(uniqueTexts);

        HuffmanEncodedTextArray<string> encodedTexts = encoder.EncodeTexts(
            uniqueTexts.Select(static text => (text, text)),
            endian);

        return new HuffmanEncodingResult(encodedTexts, tree);
    }

    /// <summary>
    ///     Gets the number of bytes needed for <paramref name="numberOfBits" /> bits.
    /// </summary>
    /// <param name="numberOfBits">The number of bits.</param>
    /// <param name="usePadding">Whether to round up to a multiple of 4 bytes.</param>
    public static int GetDataLengthInBytes(int numberOfBits, bool usePadding)
    {
        int byteSize = (numberOfBits + 7) / 8;
        int remainder = byteSize % 4;
        return usePadding && remainder != 0 ? byteSize + 4 - remainder : byteSize;
    }

    /// <summary>
    ///     Converts bits to bytes, bit <c>i</c> go to bit <c>i % 32</c> of the <c>i / 32</c>-th 32-bit integer.
    /// </summary>
    /// <param name="encodedTextsAsBoolList">The bits to convert.</param>
    /// <param name="endian">The byte order of the 32-bit integers.</param>
    /// <param name="usePadding">
    ///     Whether to round up to a multiple of 4 bytes. Big endian is always padded, because it swaps whole integers.
    /// </param>
    public static byte[] GetByteArrayForBoolList(IReadOnlyList<bool> encodedTextsAsBoolList,
        Endian endian = Endian.Little, bool usePadding = true)
    {
        ArgumentNullException.ThrowIfNull(encodedTextsAsBoolList);

        if (endian != Endian.Big)
        {
            return ToLittleEndianBytes(encodedTextsAsBoolList, usePadding);
        }

        byte[] byteArray = ToLittleEndianBytes(encodedTextsAsBoolList, true);

        Span<uint> integers = MemoryMarshal.Cast<byte, uint>(byteArray);
        BinaryPrimitives.ReverseEndianness(integers, integers);

        return byteArray;
    }

    /// <summary>
    ///     Builds the Huffman tree for <paramref name="texts" /> and keeps it for the encode methods.
    /// </summary>
    /// <remarks>
    ///     The tree also holds the end delimiter <c>'\0'</c>, counted once per text.
    /// </remarks>
    /// <param name="texts">The texts to encode, or texts with the same char frequencies.</param>
    /// <returns>The node values of the tree, in the order they are written.</returns>
    public IReadOnlyList<uint> BuildHuffmanEncodingTree(IEnumerable<string> texts)
    {
        List<string> strings = [.. texts];
        HuffmanConstructionNode rootNode = CalculateHuffmanEncoding(strings);

        List<HuffmanNode> encodingNodes = GetNodeListToWrite(rootNode);
        _characterEncoding = GetCharEncoding(encodingNodes);

        return encodingNodes.ConvertAll(static node => node.Value);
    }

    /// <summary>
    ///     Encodes one text with the tree from <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" />.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="includeEndDelimiter">Whether to add the code of the end delimiter <c>'\0'</c>.</param>
    /// <returns>The code of the text, one bool per bit.</returns>
    /// <exception cref="KeyNotFoundException">A char of <paramref name="text" /> is not in the tree.</exception>
    /// <exception cref="InvalidOperationException">No tree was built yet.</exception>
    public IReadOnlyList<bool> EncodeText(string text, bool includeEndDelimiter = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        CheckEncodingExists();

        List<bool> encodedText = [];
        AppendEncodedText(encodedText, text, _characterEncoding, includeEndDelimiter);
        return encodedText;
    }

    /// <summary>
    ///     Encodes texts with the tree from <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" />,
    ///     and converts them to bytes when <paramref name="endian" /> is given.
    /// </summary>
    /// <typeparam name="T">The type of the text identifiers, unique per text.</typeparam>
    /// <param name="textsPerIdentifier">The texts to encode, each with its identifier.</param>
    /// <param name="endian">
    ///     The byte order of the bytes in <see cref="HuffmanEncodedTextArray{T}.EncodedTexts" />, or
    ///     <see langword="null" /> to only encode to bits, like <see cref="EncodeTextsToBool{T}" />.
    /// </param>
    /// <param name="compressResults">Whether texts that are equal share one code.</param>
    /// <param name="usePadding">Whether to round the bytes up to a multiple of 4. Big endian always pads.</param>
    /// <returns>The encoded texts and the bit position of each, in the order given.</returns>
    /// <exception cref="KeyNotFoundException">A char of a text is not in the tree.</exception>
    /// <exception cref="InvalidOperationException">No tree was built yet.</exception>
    /// <seealso cref="GetByteArrayForBoolList" />
    public HuffmanEncodedTextArray<T> EncodeTexts<T>(IEnumerable<(T Identifier, string Text)> textsPerIdentifier,
        Endian? endian, bool compressResults = false, bool usePadding = true) where T : notnull
    {
        HuffmanEncodedTextArray<T> byteListResult = EncodeTextsToBool(textsPerIdentifier, compressResults);

        if (endian is null)
        {
            return byteListResult;
        }

        byteListResult.EncodedTexts =
            GetByteArrayForBoolList(byteListResult.EncodedTextsAsBoolList, endian.Value, usePadding);

        return byteListResult;
    }

    /// <summary>
    ///     Encodes texts to bits with the tree from <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" />.
    /// </summary>
    /// <remarks><see cref="HuffmanEncodedTextArray{T}.EncodedTexts" /> stays <see langword="null" />.</remarks>
    /// <typeparam name="T">The type of the text identifiers, unique per text.</typeparam>
    /// <param name="textsPerIdentifier">The texts to encode, each with its identifier.</param>
    /// <param name="compressResults">Whether texts that are equal share one code.</param>
    /// <returns>The encoded texts and the bit position of each, in the order given.</returns>
    /// <exception cref="KeyNotFoundException">A char of a text is not in the tree.</exception>
    /// <exception cref="InvalidOperationException">No tree was built yet.</exception>
    public HuffmanEncodedTextArray<T> EncodeTextsToBool<T>(IEnumerable<(T Identifier, string Text)> textsPerIdentifier,
        bool compressResults = false) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(textsPerIdentifier);

        CheckEncodingExists();

        List<(T Identifier, int Position)> positionsOfStrings = [];
        List<bool> encodedTextBoolList = [];

        Dictionary<string, int> alreadyEncodedTextPositions = [];

        foreach ((T textIdentifier, string text) in textsPerIdentifier)
        {
            int position = encodedTextBoolList.Count;

            if (compressResults)
            {
                if (alreadyEncodedTextPositions.TryGetValue(text, out int existingPosition))
                {
                    positionsOfStrings.Add((textIdentifier, existingPosition));
                    continue;
                }

                alreadyEncodedTextPositions.Add(text, position);
            }

            positionsOfStrings.Add((textIdentifier, position));
            AppendEncodedText(encodedTextBoolList, text, _characterEncoding, true);
        }

        return new HuffmanEncodedTextArray<T>(positionsOfStrings, encodedTextBoolList);
    }

    /// <summary>
    ///     Builds the Huffman tree for the char frequencies of <paramref name="texts" /> and returns its root.
    /// </summary>
    private static HuffmanConstructionNode CalculateHuffmanEncoding(List<string> texts)
    {
        // The nodes start in insertion order and List.Sort is not stable,
        // so the order of the texts can change which equal nodes merge first
        Dictionary<char, int> charNumbers = [];
        // Hot path, runs once per char, keep it a plain loop instead of LINQ
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (string text in texts)
        {
            foreach (char c in text)
            {
                CollectionsMarshal.GetValueRefOrAddDefault(charNumbers, c, out _)++;
            }
        }

        charNumbers[Delimiter] = texts.Count;

        List<HuffmanConstructionNode> nodeList = [with(charNumbers.Count)];
        foreach ((char key, int value) in charNumbers)
        {
            nodeList.Add(new HuffmanConstructionNode(key, value));
        }

        // TODO:
        // Note: Found with AI
        // When the delimiter is the only symbol (every text is empty), the root is a leaf, the tree is empty
        // and encoding throws KeyNotFoundException. Check what tree the game writes for that case.
        uint nodeValue = 0;
        while (nodeList.Count > 1)
        {
            nodeList.Sort();

            HuffmanConstructionNode left = nodeList[0];
            HuffmanConstructionNode right = nodeList[1];

            nodeList.RemoveRange(0, 2);

            HuffmanConstructionNode composite = new() { Value = nodeValue++ };
            composite.SetLeftNode(left);
            composite.SetRightNode(right);

            nodeList.Add(composite);
        }

        return nodeList[0];
    }

    /// <summary>
    ///     Gets the code of every leaf in <paramref name="encodingNodes" />.
    /// </summary>
    private static Dictionary<char, List<bool>> GetCharEncoding(List<HuffmanNode> encodingNodes)
    {
        Dictionary<char, List<bool>> charEncodings = [];

        foreach (HuffmanNode node in encodingNodes.Where(static node => node.IsLeaf))
        {
            charEncodings.Add(node.Letter, GetCharEncodingRecursive(node));
        }

        return charEncodings;
    }

    /// <summary>
    ///     Flattens the tree into the order it is written: the two children of each branch, by branch value.
    /// </summary>
    /// <remarks>The root itself is not written.</remarks>
    private static List<HuffmanNode> GetNodeListToWrite(HuffmanNode rootNode)
    {
        List<HuffmanNode> branches = [];
        Stack<HuffmanNode> pending = new([rootNode]);
        while (pending.TryPop(out HuffmanNode? node))
        {
            if (node is not { Left: { } left, Right: { } right })
            {
                continue;
            }

            branches.Add(node);
            pending.Push(left);
            pending.Push(right);
        }

        // Branch values are unique, so the order they were found in does not matter
        branches.Sort();

        List<HuffmanNode> nodesSansRoot = [with(branches.Count * 2)];
        foreach (HuffmanNode branch in branches)
        {
            nodesSansRoot.Add(branch.Left!);
            nodesSansRoot.Add(branch.Right!);
        }

        return nodesSansRoot;
    }

    /// <summary>
    ///     Gets the path from the root to <paramref name="node" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">A node is not a child of its parent.</exception>
    private static List<bool> GetCharEncodingRecursive(HuffmanNode node)
    {
        HuffmanNode? parent = node.Parent;
        if (parent is null)
        {
            return [];
        }

        List<bool> encoding = GetCharEncodingRecursive(parent);

        if (node == parent.Left)
        {
            encoding.Add(false);
        }
        else if (node == parent.Right)
        {
            encoding.Add(true);
        }
        else
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Trying to find encoding for node <{node}> failed due to incorrectly setup encoding tree!"));
        }

        return encoding;
    }

    /// <summary>
    ///     Appends the code of each char of <paramref name="text" />, then optionally the code of the end delimiter.
    /// </summary>
    /// <exception cref="KeyNotFoundException">A char is not in <paramref name="charEncoding" />.</exception>
    private static void AppendEncodedText(List<bool> output, string text,
        Dictionary<char, List<bool>> charEncoding, bool includeEndDelimiter)
    {
        foreach (char c in text)
        {
            output.AddRange(GetCharacterEncoding(c, charEncoding));
        }

        if (includeEndDelimiter)
        {
            output.AddRange(GetCharacterEncoding(Delimiter, charEncoding));
        }
    }

    /// <exception cref="KeyNotFoundException"><paramref name="c" /> is not in <paramref name="charEncoding" />.</exception>
    private static List<bool> GetCharacterEncoding(char c, Dictionary<char, List<bool>> charEncoding)
    {
        if (charEncoding.TryGetValue(c, out List<bool>? encodedChar))
        {
            return encodedChar;
        }

        if (c == Delimiter)
        {
            throw new KeyNotFoundException("Encoding does not contain mapping for end delimiter!");
        }

        throw new KeyNotFoundException(
            string.Create(CultureInfo.InvariantCulture,
                $"Encoding does not contain a mapping for symbol of value {(int)c}: '{c}'!"));
    }

    /// <summary>
    ///     Converts bits to bytes, bit <c>i</c> go to bit <c>i % 8</c> of byte <c>i / 8</c>.
    /// </summary>
    private static byte[] ToLittleEndianBytes(IReadOnlyList<bool> bits, bool usePadding)
    {
        // Filled by the constructor
        // ReSharper disable once CollectionNeverUpdated.Local
        BitArray bitArray = new([.. bits]);

        byte[] byteArray = new byte[GetDataLengthInBytes(bits.Count, usePadding)];
        bitArray.CopyTo(byteArray, 0);

        return byteArray;
    }

    /// <exception cref="InvalidOperationException">No tree was built yet.</exception>
    [MemberNotNull(nameof(_characterEncoding))]
    private void CheckEncodingExists()
    {
        if (_characterEncoding is null)
        {
            throw new InvalidOperationException(
                "Cannot encode texts before the Huffman tree was built! Call 'BuildHuffmanEncodingTree' on the encoder before attempting to encode a string!");
        }
    }
}