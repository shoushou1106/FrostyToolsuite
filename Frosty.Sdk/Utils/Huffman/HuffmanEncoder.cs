using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

/// <summary>
///     HuffmanEncoder can be used to encode the given strings into a byte array.
/// </summary>
/// <example>
///     Either call <see cref="Encode(IEnumerable{string}, Endian)" /> directly with the strings to encode,
///     or if more control is required, call <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> with a
///     representation of the strings to encode to build the encoding tree. After that, call
///     <see cref="EncodeTexts{T}(IEnumerable{Tuple{T, string}}, Endian?, bool, bool)" /> to get the encoding result.
/// </example>
public class HuffmanEncoder
{
    /// <summary>
    ///     The character encoding in dictionary form.
    /// </summary>
    private Dictionary<char, List<bool>>? _characterEncoding;

    /// <summary>
    ///     Helper method to get the encoded texts and the encoding tree all in one go.
    ///     The encoded texts, the encoding, and the individual text offsets can be retrieved from the
    ///     <see cref="EncodingResult" />.
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <c>EncodingResult.EncodingTree</c> returns the Huffman tree used for encoding in the form of
    ///                 an integer list.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description><c>EncodingResult.EncodedTexts</c> returns the given strings encoded to a byte array.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>EncodingResult.GetTextPositionsDictionary()</c> returns a dictionary with the given strings
    ///                 as keys and their bit offsets in the <c>EncodedTexts</c>.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>EncodingResult.EncodedTextPositions</c> returns the same data as above in the form of a
    ///                 list of tuples for use in iterations.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     Note that this method always uses little-endian for the text data.
    /// </summary>
    /// <param name="texts">The texts to encode.</param>
    /// <param name="inEndian">The endianness to use for the encoded texts.</param>
    /// <returns>An <see cref="EncodingResult" /> with the data.</returns>
    public static EncodingResult Encode(IEnumerable<string> texts, Endian inEndian = Endian.Little)
    {
        HashSet<string> nonNullUniqueStrings = [.. texts];

        HuffmanEncoder encoder = new();
        IList<uint> tree = encoder.BuildHuffmanEncodingTree(nonNullUniqueStrings);

        HuffmanEncodedTextArray<string> encodedTexts = encoder.EncodeTexts(
            nonNullUniqueStrings.Select(x => new Tuple<string, string>(x, x)),
            inEndian);

        return new EncodingResult(encodedTexts, tree);
    }

    /// <summary>
    ///     Computes the necessary length in bytes for the given number of bits with optionally added padding.
    /// </summary>
    /// <param name="numberOfBits">Number of bits to turn into a byte array.</param>
    /// <param name="usePadding">Whether to use padding to get to the next 32-bit or 4-byte size.</param>
    /// <returns>The number of necessary bytes to store the given number of bits with the given padding option.</returns>
    public static int GetDataLengthInBytes(int numberOfBits, bool usePadding)
    {
        int byteSize = Math.DivRem(numberOfBits, 8, out int remainder);
        if (remainder != 0)
        {
            byteSize++;
        }

        if (!usePadding)
        {
            return byteSize;
        }

        int paddingLength = (4 - byteSize) & 3;
        // paddingLength is computed as a modulo 4 operation; just using bitwise AND here.
        byteSize += paddingLength;

        return byteSize;
    }

    /// <summary>
    ///     Returns the byte array for the given bool list with the specified endianness and padding. Some things to note:
    ///     <list type="bullet">
    ///         <item>
    ///             The byte array returns the bool list as 32-bit integer blocks; the endianness is applied to this integer
    ///             representation.
    ///         </item>
    ///         <item>
    ///             Endianness behavior overrides padding behavior. The default is a little-endian byte array; if this is
    ///             changed to big-endian, then padding is applied regardless of the argument.
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="encodedTestsAsBoolList">The list of bools to convert to a byte array.</param>
    /// <param name="endian">The endianness to apply to the (integer) representation of the bool array.</param>
    /// <param name="usePadding">
    ///     Whether to pad the result to get to the next 4-byte length. If big endian is used, then the result is always
    ///     padded.
    /// </param>
    /// <returns>A byte array with the given boolean list as a byte representation.</returns>
    public static byte[] GetByteArrayForBoolList(IList<bool> encodedTestsAsBoolList,
        Endian endian = Endian.Little, bool usePadding = true)
    {
        ArgumentNullException.ThrowIfNull(encodedTestsAsBoolList);

        // BitArray is always little-endian by default!
        if (endian != Endian.Big)
        {
            return GetByteArrayForBoolList0(encodedTestsAsBoolList, usePadding);
        }

        // Change of endianness forces padding:
        byte[] byteArray = GetByteArrayForBoolList0(encodedTestsAsBoolList, true);

        for (int index = 0; index + 4 <= byteArray.Length; index += 4)
        {
            Span<byte> slice = byteArray.AsSpan(index, 4);
            uint number = BitConverter.ToUInt32(slice);
            uint reverseNumber = BinaryPrimitives.ReverseEndianness(number);
            BitConverter.TryWriteBytes(slice, reverseNumber);
        }

        // ReSharper disable once GrammarMistakeInComment
        // This should no longer be necessary when padding is applied beforehand
        // if (index < littleEndianByteArray.Length)
        // {
        //     byte[] intermediate = new byte[4];
        //
        //     for (int i = 0; i < 4 && index < littleEndianByteArray.Length; i++)
        //     {
        //         intermediate[i] = littleEndianByteArray[index];
        //         index++;
        //     }
        //     uint number = BitConverter.ToUInt32(intermediate);
        //     uint reverseNumber = BinaryPrimitives.ReverseEndianness(number);
        //
        //     byte[] reverseBytes = BitConverter.GetBytes(reverseNumber);
        //     reverseList.AddRange(reverseBytes);
        // }

        return byteArray;
    }

    /// <summary>
    ///     Uses the given input to construct the Huffman encoding table (or tree). The created encoding includes an end
    ///     delimiter
    ///     character (char 0x0) with the number of occurrences equal to the number of given strings.
    ///     Also, temporarily stores the given texts to use them in other methods if the need arises.
    ///     Returns the uint representation of the Huffman encoding.
    ///     Note that this method is quite a bit slower than calling
    ///     <see cref="EncodeTexts{T}(IEnumerable{Tuple{T, string}}, Endian?, bool, bool)" />, but is null- and duplicate-safe
    ///     and pads the result data.
    /// </summary>
    /// <param name="texts">The texts to encode, or a suitable approximation of the characters' appearances.</param>
    /// <returns>The list of Huffman node values in the order they should be written.</returns>
    public IList<uint> BuildHuffmanEncodingTree(IEnumerable<string> texts)
    {
        List<string> strings = [.. texts];
        HuffmanConstructionNode rootNode = CalculateHuffmanEncoding(strings);

        List<HuffmanNode> encodingNodes = GetNodeListToWrite(rootNode);
        _characterEncoding = GetCharEncoding(encodingNodes);

        return [.. encodingNodes.Select(node => node.Value)];
    }

    /// <summary>
    ///     Encodes the given text into a list of bool values, using the encoding set previously by
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" />.
    ///     The resulting bool list can, e.g., be put into a BitArray by calling <c>new BitArray(resultList.ToArray())</c>.
    ///     From there, it can be copied into byte arrays or similar.
    /// </summary>
    /// <param name="text">A single text to encode.</param>
    /// <param name="includeEndDelimiter">
    ///     Whether the encoding for a 0x0 character should be added to the end of the text.
    /// </param>
    /// <returns>The bool representation of the encoded text.</returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary.</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public IList<bool> EncodeText(string text, bool includeEndDelimiter = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        CheckEncodingExists();
        return GetEncodedText(text, _characterEncoding, includeEndDelimiter);
    }

    /// <summary>
    ///     Encodes the given string using the previously created Huffman encoding from
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> and returns the created byte array together with the
    ///     list of text identifiers and their bit offsets in the byte array.
    ///     NOTE: Changing the encoding to big-endian might invalidate the assigned positions for the text in the returned
    ///     result!
    ///     <seealso cref="EncodeTextsToBool{T}(IEnumerable{Tuple{T, string}}, bool)" />
    ///     <seealso cref="GetByteArrayForBoolList(IList{bool}, Endian, bool)" />
    /// </summary>
    /// <typeparam name="T">
    ///     The type of identifier to use for the texts. Might be an uint id/counter/hash value or complex type.
    ///     Must be unique per string and not null.
    /// </typeparam>
    /// <param name="textsPerIdentifier">The tuples of string identifiers and the strings to encode.</param>
    /// <param name="endian">
    ///     The endianness to use for creating the byte array in the returned result object.
    ///     If no endian is given, then the result will not include the byte array and will be the same as created by
    ///     <c>EncodeTextsToBool</c>! You can call <see cref="GetByteArrayForBoolList(IList{bool}, Endian, bool)" /> with the
    ///     desired settings later.
    ///     Note that the given endian overrides any behavior from <c>usePadding</c>. If the given endian is not null but
    ///     different from the system setting, then the result is always padded.
    ///     Furthermore, note that big-endian settings might mean the returned bit offsets for texts in the result will no
    ///     longer
    ///     be valid on the target system!
    /// </param>
    /// <param name="compressResults">
    ///     If true, then this method tries to reuse already-compiled string encodings and produced
    ///     bit segments, so the returned byte array might be shorter. Defaults to false.
    /// </param>
    /// <param name="usePadding">
    ///     Whether to use padding to increase the data byte array size so that it is a multiple of 4.
    ///     I.e., multiples of 32 bits. This behavior is overridden by the given <c>endian</c>. An endianness different from
    ///     the
    ///     system default will always be padded.
    /// </param>
    /// <returns>
    ///     An instance of <see cref="HuffmanEncodedTextArray{T}" /> with the byte array of the encoded texts and a list
    ///     of the given text identifiers and their bit positions inside the byte array. The list has the same ordering as the
    ///     given input to this method.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary.</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public HuffmanEncodedTextArray<T> EncodeTexts<T>(IEnumerable<Tuple<T, string>> textsPerIdentifier, Endian? endian,
        bool compressResults = false, bool usePadding = true) where T : notnull
    {
        HuffmanEncodedTextArray<T> byteListResult = EncodeTextsToBool(textsPerIdentifier, compressResults);

        if (endian is null)
        {
            return byteListResult;
        }

        byte[] encodedToBytes =
            GetByteArrayForBoolList(byteListResult.EncodedTestsAsBoolList, endian.Value, usePadding);
        byteListResult.EncodedTexts = encodedToBytes;

        return byteListResult;
    }

    /// <summary>
    ///     Encodes the given string using the previously created Huffman encoding from
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> and returns the list of bools for the encoded texts.
    ///     Note that this method replaces null strings with empty strings.
    ///     NOTE: The returned <see cref="HuffmanEncodedTextArray{T}" /> will not have the byte array set, only the bool list!
    /// </summary>
    /// <typeparam name="T">
    ///     The type of identifier to use for the texts. Might be an uint id/counter/hash value or complex type.
    ///     Must be unique per string and not null.
    /// </typeparam>
    /// <param name="textsPerIdentifier">The tuples of string identifiers and the strings to encode.</param>
    /// <param name="compressResults">
    ///     If true, then this method tries to reuse already-compiled string encodings and produced
    ///     bit segments, so the returned byte array might be shorter. Defaults to false.
    /// </param>
    /// <returns>
    ///     An instance of <see cref="HuffmanEncodedTextArray{T}" /> with the bool list of the encoded texts and a list
    ///     of the given text identifiers and their bit positions inside the list. The list has the same ordering as the given
    ///     input to this method.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary.</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public HuffmanEncodedTextArray<T> EncodeTextsToBool<T>(IEnumerable<Tuple<T, string>> textsPerIdentifier,
        bool compressResults = false) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(textsPerIdentifier);

        CheckEncodingExists();

        List<IdentifierPositionTuple<T>> positionsOfStrings = [];
        List<bool> encodedTextBoolList = [];

        Dictionary<string, int> alreadyEncodedTextPositions = [];

        foreach ((T textIdentifier, string rawText) in textsPerIdentifier)
        {
            string text = string.IsNullOrEmpty(rawText) ? string.Empty : rawText;
            int position = encodedTextBoolList.Count;

            bool encodeText = true;
            if (compressResults)
            {
                if (alreadyEncodedTextPositions.TryGetValue(text, out int existingPos))
                {
                    position = existingPos;
                    encodeText = false;
                }
                else
                {
                    alreadyEncodedTextPositions[text] = position;
                }
            }

            positionsOfStrings.Add(new IdentifierPositionTuple<T>(textIdentifier, position));

            if (encodeText)
            {
                encodedTextBoolList.AddRange(GetEncodedText(text, _characterEncoding, true));
            }
        }

        return new HuffmanEncodedTextArray<T>(positionsOfStrings, encodedTextBoolList);
    }

    /// <summary>
    ///     Resets instance variables.
    /// </summary>
    public void Dispose()
    {
        _characterEncoding = null;
    }

    /// <summary>
    ///     Calculates the Huffman encoding for the given texts and returns the root node of the resulting tree.
    /// </summary>
    /// <param name="texts">The texts to calculate the encoding for.</param>
    /// <returns>The Huffman root node.</returns>
    private static HuffmanConstructionNode CalculateHuffmanEncoding(List<string> texts)
    {
        // Get the set of chars and their number of occurrences...
        Dictionary<char, int> charNumbers = [];
        foreach (char c in texts.SelectMany(text => text))
        {
            if (charNumbers.TryGetValue(c, out int occurrences))
            {
                charNumbers[c] = ++occurrences;
            }
            else
            {
                charNumbers[c] = 1;
            }
        }

        // Add the text delimiter:
        const char delimiter = (char)0x0;
        charNumbers[delimiter] = texts.Count;

        List<HuffmanConstructionNode> nodeList = new(charNumbers.Count);
        foreach ((char key, int value) in charNumbers)
        {
            nodeList.Add(new HuffmanConstructionNode(key, value));
        }

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
    ///     Returns a dictionary of characters to their encoded bit representations.
    /// </summary>
    /// <param name="encodingNodes">The (limited) list of Huffman code nodes used.</param>
    /// <returns>A dictionary of char-to-code values.</returns>
    private static Dictionary<char, List<bool>> GetCharEncoding(List<HuffmanNode> encodingNodes)
    {
        Dictionary<char, List<bool>> charEncodings = [];

        foreach (HuffmanNode node in encodingNodes)
        {
            if (node.Left is not null || node.Right is not null)
            {
                continue;
            }

            char c = node.Letter;
            List<bool> path = GetCharEncodingRecursive(node);

            charEncodings.Add(c, path);
        }

        return charEncodings;
    }

    /// <summary>
    ///     Recalculates the node list to write to the resource based on the root node.
    ///     This method returns a flat list of Huffman nodes that does not include the given root node.
    /// </summary>
    /// <param name="rootNode">The root node of the tree to flatten into a single list.</param>
    /// <returns>A list of nodes in the order to write.</returns>
    private static List<HuffmanNode> GetNodeListToWrite(HuffmanNode? rootNode)
    {
        List<HuffmanNode> nodesSansRoot = [];

        if (rootNode is null)
        {
            return nodesSansRoot;
        }

        // Get all branches
        List<HuffmanNode> branches = GetAllBranchNodes([rootNode]);

        // Sort branches by their value so that they can be written out in the correct order
        branches.Sort();

        // Add all the children in the order of their parent's value
        // GetAllBranchNodes only returns nodes with both children set
        foreach (HuffmanNode branch in branches)
        {
            nodesSansRoot.Add(branch.Left!);
            nodesSansRoot.Add(branch.Right!);
        }

        return nodesSansRoot;
    }

    private static List<HuffmanNode> GetAllBranchNodes(List<HuffmanNode> currentNodes)
    {
        List<HuffmanNode> branchNodes = [];

        foreach (HuffmanNode currentNode in currentNodes)
        {
            if (currentNode.Left is null || currentNode.Right is null)
            {
                continue;
            }

            branchNodes.Add(currentNode);
            branchNodes.AddRange(
                GetAllBranchNodes([currentNode.Left, currentNode.Right]));
        }

        return branchNodes;
    }

    /// <summary>
    ///     Returns the encoding for the given node as a path in the tree.
    /// </summary>
    /// <param name="node">The node for which to find the encoding.</param>
    /// <returns>The encoding as a list of booleans.</returns>
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
                $"Trying to find encoding for node <{node}> failed due to incorrectly setup encoding tree!");
        }

        return encoding;
    }

    /// <summary>
    ///     Returns the bit-encoded text as a list of booleans.
    ///     The end of the text is marked with the delimiter character 0x0 / Huffman node value = uint.MaxValue.
    /// </summary>
    /// <param name="toEncode">The text to encode, given as a char array or similar char enumerable.</param>
    /// <param name="charEncoding">The character encoding to use for the text.</param>
    /// <param name="includeEndDelimiter">Whether to include the end delimiter in the returned encoding.</param>
    /// <returns>The encoded text.</returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary.</exception>
    private static List<bool> GetEncodedText(IEnumerable<char> toEncode, Dictionary<char, List<bool>> charEncoding,
        bool includeEndDelimiter)
    {
        List<bool> encodedText = [];

        foreach (char c in toEncode)
        {
            encodedText.AddRange(TryGetCharacterEncoding(c, charEncoding));
        }

        if (!includeEndDelimiter)
        {
            return encodedText;
        }

        const char delimiter = (char)0x0;
        encodedText.AddRange(TryGetCharacterEncoding(delimiter, charEncoding));

        return encodedText;
    }

    private static List<bool> TryGetCharacterEncoding(char c, Dictionary<char, List<bool>> charEncoding)
    {
        if (charEncoding.TryGetValue(c, out List<bool>? encodedChar))
        {
            return encodedChar;
        }

        if (c == 0x0)
        {
            throw new KeyNotFoundException("Encoding does not contain mapping for end delimiter!");
        }

        string errorMessage = $"Encoding does not contain a mapping for symbol of value {(int)c}: '{c}'!";
        throw new KeyNotFoundException(errorMessage);
    }

    /// <summary>
    ///     Returns the byte array for the given bool list. Does not take encoding into account!
    ///     According to the BitArray docs, the result is always in little-endian.
    /// </summary>
    private static byte[] GetByteArrayForBoolList0(IList<bool> encodedTestsAsBoolList, bool usePadding)
    {
        int byteSize = GetDataLengthInBytes(encodedTestsAsBoolList.Count, usePadding);

        // Filled by the constructor
        // ReSharper disable once CollectionNeverUpdated.Local
        BitArray ba = new([.. encodedTestsAsBoolList]);

        byte[] byteArray = new byte[byteSize];

        ba.CopyTo(byteArray, 0);

        return byteArray;
    }

    /// <summary>
    ///     Asserts that an encoding has been created.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no encoding exists.</exception>
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