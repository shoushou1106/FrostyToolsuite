using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils.Huffman;

/// <summary>
///     Huffman encoded texts and the bit position of each text in them.
/// </summary>
/// <typeparam name="T">The type of identifier used for the texts.</typeparam>
public class HuffmanEncodedTextArray<T> where T : notnull
{
    private Dictionary<T, int>? _positionsDictionary;

    public HuffmanEncodedTextArray(IReadOnlyList<(T Identifier, int Position)> encodedTextPositions,
        IReadOnlyList<bool> encodedTextsAsBoolList)
    {
        EncodedTextPositions = encodedTextPositions;
        EncodedTextsAsBoolList = encodedTextsAsBoolList;
    }

    /// <summary>
    ///     Copies <paramref name="original" />, sharing its lists and its byte array.
    /// </summary>
    protected HuffmanEncodedTextArray(HuffmanEncodedTextArray<T> original)
    {
        ArgumentNullException.ThrowIfNull(original);

        EncodedTextPositions = original.EncodedTextPositions;
        EncodedTextsAsBoolList = original.EncodedTextsAsBoolList;
        EncodedTexts = original.EncodedTexts;
        _positionsDictionary = original._positionsDictionary;
    }

    /// <summary>
    ///     Each text identifier with the bit position of its text, in the order the texts were given.
    /// </summary>
    public IReadOnlyList<(T Identifier, int Position)> EncodedTextPositions { get; }

    /// <summary>
    ///     All encoded texts, one bool per bit.
    /// </summary>
    public IReadOnlyList<bool> EncodedTextsAsBoolList { get; }

    /// <summary>
    ///     All encoded texts as bytes, or <see langword="null" /> until an endianness was given.
    /// </summary>
    /// <seealso cref="CreateEncodedTexts" />
    // CA1819: Properties should not return arrays
#pragma warning disable CA1819
    public byte[]? EncodedTexts { get; internal set; }
#pragma warning restore CA1819

    /// <summary>
    ///     Gets <see cref="EncodedTextPositions" /> as a dictionary from identifier to bit position.
    /// </summary>
    public IReadOnlyDictionary<T, int> GetTextPositionsDictionary()
    {
        return _positionsDictionary ??=
            EncodedTextPositions.ToDictionary(static entry => entry.Identifier, static entry => entry.Position);
    }

    /// <summary>
    ///     Converts <see cref="EncodedTextsAsBoolList" /> to bytes and stores them in <see cref="EncodedTexts" />.
    /// </summary>
    /// <seealso cref="HuffmanEncoder.GetByteArrayForBoolList" />
    public byte[] CreateEncodedTexts(Endian endian = Endian.Little, bool usePadding = true)
    {
        EncodedTexts = HuffmanEncoder.GetByteArrayForBoolList(EncodedTextsAsBoolList, endian, usePadding);
        return EncodedTexts;
    }
}