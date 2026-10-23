using System.Buffers.Binary;
using System.Collections;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

/// <summary>
///     HuffmanEncoder can be used to encode given strings into a byte array.
/// </summary>
/// <example>
///     Either call <see cref="Encode(IEnumerable{string}, Endian)" /> directly with the strings to encode.
///     Or if more control is required, call <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> with a
///     representation of the strings to encode to build the encoding tree. After that call
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
    ///     The encoded texts, the encoding, and the individual texts offsets can be retrieved from the
    ///     <see cref="EncodingResult" />.
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <c>EncodingResult.EncodingTree</c> returns the huffman tree used for encoding in the form of
    ///                 an integer list.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description><c>EncodingResult.EncodedTexts</c> returns the given strings encoded to a byte array..</description>
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
    ///     Note that this method always uses little endinan for the text data.
    /// </summary>
    /// <param name="texts">The texts to encode</param>
    /// <param name="inEndian">The endian to use for the encoded texts</param>
    /// <returns>EncodingResult with the data.</returns>
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
    /// <param name="numberOfBits">Number of bits to turn into a byte array</param>
    /// <param name="usePadding">Whether or not to use padding to get to the next 32 bits or 4 byte size</param>
    /// <returns>the number of necessary bytes to store the given number of bits with the given padding option</returns>
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
        // paddingLength is computed as modulo 4 operation, just uing bitwise and here.
        byteSize += paddingLength;

        return byteSize;
    }

    /// <summary>
    ///     Returns the byte array for the given bool list in the specified endianness and padding. Some things to note:
    ///     <list type="bullet">
    ///         <item>
    ///             The byte array returns the bools as 32 bit integer blocks, the endianness is applied to this integer
    ///             representation.
    ///         </item>
    ///         <item>
    ///             Endianess behabiour overwrites padding behaviour. The default is a little endian byte array, if this is
    ///             changed to big endian than padding is applied regardless of the argument.
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="encodedTestsAsBools">The list of bools to convert to byte array.</param>
    /// <param name="endian">The endianness to apply to the (integer) representation of the bool array.</param>
    /// <param name="usePadding">
    ///     Whether or not to use padd the result to get to the next 4 bytes length. If big endian is
    ///     used, then the result is always padded.
    /// </param>
    /// <returns>A byte array with the given boolean list as byte representation</returns>
    public static byte[] GetByteArrayForBoolList(IList<bool> encodedTestsAsBools,
        Endian endian = Endian.Little, bool usePadding = true)
    {
        ArgumentNullException.ThrowIfNull(encodedTestsAsBools);

        // BitArray is always little endian by default!
        if (endian != Endian.Big)
        {
            return GetByteArrayForBoolList0(encodedTestsAsBools, usePadding);
        }

        // Change of endianness forces padding:
        byte[] byteArray = GetByteArrayForBoolList0(encodedTestsAsBools, true);

        for (int index = 0; index + 4 <= byteArray.Length; index += 4)
        {
            Span<byte> slice = byteArray.AsSpan(index, 4);
            uint number = BitConverter.ToUInt32(slice);
            uint reverseNumber = BinaryPrimitives.ReverseEndianness(number);
            BitConverter.TryWriteBytes(slice, reverseNumber);
        }

        // This should no longer be necessary, when calling padding before
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
    ///     Uses the given input to construct the huffman encoding table (or tree). The created encoding includes end delimiter
    ///     character (char 0x0) with a number of occurrences of the number of given strings.
    ///     Also temporarily stores the given texts to use them in other methods if the need arises.
    ///     Returns the uint representation of the huffman encoding.
    ///     Note that this method is quite a bit slower than calling
    ///     <see cref="EncodeTexts{T}(IEnumerable{Tuple{T, string}}, Endian?, bool, bool)" />, but is null and duplicate save
    ///     as well as padding the result data.
    /// </summary>
    /// <param name="texts">The texts to encode, or a suitable approximation of the characters appearances.</param>
    /// <returns>The list of huffman node values in the order they should be written.</returns>
    public IList<uint> BuildHuffmanEncodingTree(IEnumerable<string> texts)
    {
        List<string> strings = [.. texts];
        HuffManConstructionNode rootNode = CalculateHuffmanEncoding(strings);

        List<HuffmanNode> encodingNodes = GetNodeListToWrite(rootNode);
        _characterEncoding = GetCharEncoding(encodingNodes);

        return [.. encodingNodes.Select(node => node.Value)];
    }

    /// <summary>
    ///     Encodes the given text into a bool values, using the encoding set previously by
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" />.
    ///     The result bool list can e.g., be put into an BitArray by calling <c>new BitArray(resultList.ToArray())</c>. From
    ///     there it can be copied into byte arrays or similar.
    /// </summary>
    /// <param name="text">A single text to encode.</param>
    /// <param name="includeEndDelimeter">
    ///     Whether or not the encoding for a 0x0 character should be added to the end of the text.
    /// </param>
    /// <returns>the bool representation of the encoded text.</returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public IList<bool> EncodeText(string text, bool includeEndDelimeter = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        CheckEncodingExists();
        return GetEncodedText(text, _characterEncoding!, includeEndDelimeter);
    }

    /// <summary>
    ///     Encodes the given String using the previously created Huffman Encoding from
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> and returns the created byte array together with the
    ///     list of text identifiers and their bit offsets in the byte array.
    ///     NOTE: Changing the encoding to big endian might invalidate the assigned positions for the text in the returned
    ///     result!
    ///     <seealso cref="EncodeTextsToBool{T}(IEnumerable{Tuple{T, string}}, bool)" />
    ///     <seealso cref="GetByteArrayForBoolList(IList{bool}, Endian, bool)" />
    /// </summary>
    /// <typeparam name="T">
    ///     The type of identifier to use for the texts. Might be a uint id/counter/hashvalue or complex type.
    ///     Has to be unique per string and not null
    /// </typeparam>
    /// <param name="textsPerIdentifier">The tuples of string identifiers and the strings to encode.</param>
    /// <param name="endian">
    ///     The endian type to use for creating the byte array in the returned result object.
    ///     If no endian is given then the result will not include the byte array and will be the same as created by
    ///     <c>EncodeTextsToBool</c>! You can call <see cref="GetByteArrayForBoolList(IList{bool}, Endian, bool)" /> with the
    ///     wanted settings later.
    ///     Note that the given endian overwrites any behaviour from <c>usePadding</c>. If the given encoding is not null but
    ///     different from the system setting then the result is always padded.
    ///     Note furthermore that big endian settings migh mean the returned bit offsets for texts in the result will no longer
    ///     be valid on the target system!
    /// </param>
    /// <param name="compressResults">
    ///     If true, then this method tries to reuse already compiled string encodings and produced
    ///     bit segments, so the returned byte array might be shorter. Defaults to false.
    /// </param>
    /// <param name="usePadding">
    ///     Whether to use padding to get to increase the data byte array size so it is a multiple of 4.
    ///     I.e., multiples of 32 bit. This behaviur is overwritten by the given <c>endian</c>. Endians different than the
    ///     system default will always be padded.
    /// </param>
    /// <returns>
    ///     An instance of <see cref="HuffmanEncodedTextArray{T}" /> with the byte array of the encoded texts, and a list
    ///     of the given text identifiers and their bit position inside the byte array. The list has the same ordering as the
    ///     given input to this method.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public HuffmanEncodedTextArray<T> EncodeTexts<T>(IEnumerable<Tuple<T, string>> textsPerIdentifier, Endian? endian,
        bool compressResults = false, bool usePadding = true) where T : notnull
    {
        HuffmanEncodedTextArray<T> byteListResult = EncodeTextsToBool(textsPerIdentifier, compressResults);

        if (endian is null)
        {
            return byteListResult;
        }

        byte[] encodedToBytes = GetByteArrayForBoolList(byteListResult.EncodedTestsAsBools, endian.Value, usePadding);
        byteListResult.EncodedTexts = encodedToBytes;

        return byteListResult;
    }

    /// <summary>
    ///     Encodes the given String using the previously created Huffman Encoding from
    ///     <see cref="BuildHuffmanEncodingTree(IEnumerable{string})" /> and returns the List of bools for the encoded texts.
    ///     Note that this method replaces null strings with empty strings.
    ///     NOTE: The returned <see cref="HuffmanEncodedTextArray{T}" /> will not have the byte array set, only the bool list!
    /// </summary>
    /// <typeparam name="T">
    ///     The type of identifier to use for the texts. Might be a uint id/counter/hashvalue or complex type.
    ///     Has to be unique per string and not null
    /// </typeparam>
    /// <param name="textsPerIdentifier">The tuples of string identifiers and the strings to encode.</param>
    /// <param name="compressResults">
    ///     If true, then this method tries to reuse already compiled string encodings and produced
    ///     bit segments, so the returned byte array might be shorter. Defaults to false.
    /// </param>
    /// <returns>
    ///     An instance of <see cref="HuffmanEncodedTextArray{T}" /> with the bool list of the encoded texts, and a list
    ///     of the given text identifiers and their bit position inside the list. The list has the same ordering as the given
    ///     input to this method.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary</exception>
    /// <exception cref="InvalidOperationException">If no encoding has been created yet.</exception>
    public HuffmanEncodedTextArray<T> EncodeTextsToBool<T>(IEnumerable<Tuple<T, string>> textsPerIdentifier,
        bool compressResults = false) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(textsPerIdentifier);

        CheckEncodingExists();

        List<IdentifierPositionTuple<T>> positionsOfStrings = [];
        List<bool> encodedTextBools = [];

        Dictionary<string, int> alreadyEncodedTextPositions = [];

        foreach ((T textIdentifier, string rawText) in textsPerIdentifier)
        {
            string text = string.IsNullOrEmpty(rawText) ? string.Empty : rawText;
            int position = encodedTextBools.Count;

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
                encodedTextBools.AddRange(GetEncodedText(text, _characterEncoding!, true));
            }
        }

        return new HuffmanEncodedTextArray<T>(positionsOfStrings, encodedTextBools);
    }

    /// <summary>
    ///     Resets instance variables.
    /// </summary>
    public void Dispose()
    {
        _characterEncoding = null;
    }

    /// <summary>
    ///     Calculates the huffman encoding for the given texts, and returns the root node of the resulting tree.
    /// </summary>
    /// <param name="texts"></param>
    /// <returns>Huffman root node.</returns>
    private static HuffManConstructionNode CalculateHuffmanEncoding(List<string> texts)
    {
        // Get set of chars and their number of occurrences...
        Dictionary<char, int> charNumbers = [];
        foreach (char c in texts.SelectMany(text => text))
        {
            if (charNumbers.TryGetValue(c, out int occurences))
            {
                charNumbers[c] = ++occurences;
            }
            else
            {
                charNumbers[c] = 1;
            }
        }

        // Add the text delimiter:
        const char delimiter = (char)0x0;
        charNumbers[delimiter] = texts.Count;

        List<HuffManConstructionNode> nodeList = new(charNumbers.Count);
        foreach ((char key, int value) in charNumbers)
        {
            nodeList.Add(new HuffManConstructionNode(key, value));
        }

        uint nodeValue = 0;
        while (nodeList.Count > 1)
        {
            nodeList.Sort();

            HuffManConstructionNode left = nodeList[0];
            HuffManConstructionNode right = nodeList[1];

            nodeList.RemoveRange(0, 2);

            HuffManConstructionNode composite = new() { Value = nodeValue++ };
            composite.SetLeftNode(left);
            composite.SetRightNode(right);

            nodeList.Add(composite);
        }

        return nodeList[0];
    }

    /// <summary>
    ///     Returns a dictionary of characters to their encoded bit representation.
    /// </summary>
    /// <param name="encodingNodes">The (limited) list of Huffman code nodes used.</param>
    /// <returns>Dictionary of char - code values</returns>
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
    ///     Recalculates the nodelist to write to the resource based on the root node.
    ///     This method returns a flat list or huffman nodes, that do not include the given root node.
    /// </summary>
    /// <param name="rootNode">the root node of the tree to flatten into a single list.</param>
    /// <returns>list of nodes in the order to write</returns>
    private static List<HuffmanNode> GetNodeListToWrite(HuffmanNode? rootNode)
    {
        List<HuffmanNode> nodesSansRoot = [];

        if (rootNode is null)
        {
            return nodesSansRoot;
        }

        // get all branches
        List<HuffmanNode> branches = GetAllBranchNodes([rootNode]);

        // sort branches by their value, so that the write out can happen in the correct order
        branches.Sort();

        // add all the children in the order of their parent's value
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
    ///     Return the encoding for the given node as path in the tree.
    /// </summary>
    /// <param name="node">The node for which to find the encoding.</param>
    /// <returns>the encoding as list of booleans.</returns>
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
    ///     Returns the bit encoded text as list of booleans.
    ///     The end of the text is marked with the delimiter character 0x0 / huffman node value = uint.MaxValue.
    /// </summary>
    /// <param name="toEncode">The text to encode, given as char array or similar char enumerable</param>
    /// <param name="charEncoding">The character encoding to use for the text.</param>
    /// <param name="includeEndDelimeter">Whether or not to include the end delimiter in the returned encoding.</param>
    /// <returns>The encoded text.</returns>
    /// <exception cref="KeyNotFoundException">If a character or symbol to encode was not found in the dictionary</exception>
    private static List<bool> GetEncodedText(IEnumerable<char> toEncode, Dictionary<char, List<bool>> charEncoding,
        bool includeEndDelimeter)
    {
        List<bool> encodedText = [];

        foreach (char c in toEncode)
        {
            encodedText.AddRange(TryGetCharacterEncoding(c, charEncoding));
        }

        if (!includeEndDelimeter)
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
    ///     According to the BitArray docs the result is always in little endian.
    /// </summary>
    private static byte[] GetByteArrayForBoolList0(IList<bool> encodedTestsAsBools, bool usePadding)
    {
        int byteSize = GetDataLengthInBytes(encodedTestsAsBools.Count, usePadding);

        BitArray ba = new([.. encodedTestsAsBools]);

        byte[] byteArray = new byte[byteSize];

        ba.CopyTo(byteArray, 0);

        return byteArray;
    }

    /// <summary>
    ///     Asserts that an encoding has been created.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no encoding exists.</exception>
    private void CheckEncodingExists()
    {
        if (_characterEncoding is null)
        {
            throw new InvalidOperationException(
                "Cannot encode texts before the Huffman tree was built! Call 'BuildHuffmanEncodingTree' on the encoder before attempting to encode a string!");
        }
    }
}