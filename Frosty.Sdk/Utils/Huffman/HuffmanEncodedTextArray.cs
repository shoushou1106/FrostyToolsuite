using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

/// <summary>
///     Return value of the encoding function. This contains the encoded texts as a byte array, as well as the list of
///     <see cref="IdentifierPositionTuple{T}" /> that detail which text is at what bit offset inside the array.
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>HuffmanEncodedTextArray.EncodedTexts</c> returns the given strings encoded to a byte
///                 array.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>HuffmanEncodedTextArray.EncodedTextPositions</c> returns the text keys and their bit offsets
///                 in the <c>EncodedTexts</c> in the form of a list of tuples for use in iterations.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>HuffmanEncodedTextArray.GetTextPositionsDictionary()</c> returns a dictionary with the same data as
///                 above.
///             </description>
///         </item>
///     </list>
/// </summary>
/// <typeparam name="T">The type of identifier used for the texts.</typeparam>
public class HuffmanEncodedTextArray<T> where T : notnull
{
    // Dictionary representation of EncodedTextPositions created when first requested.
    private Dictionary<T, int>? _positionsDictionary;

    public HuffmanEncodedTextArray(IList<IdentifierPositionTuple<T>> inEncodedTextPositions,
        IList<bool> inEncodedTestsAsBoolList)
    {
        EncodedTextPositions = inEncodedTextPositions;
        EncodedTestsAsBoolList = inEncodedTestsAsBoolList;
    }

    /// <summary>
    ///     Copy Constructor
    /// </summary>
    /// <param name="inOriginal"></param>
    protected HuffmanEncodedTextArray(HuffmanEncodedTextArray<T> inOriginal)
    {
        ArgumentNullException.ThrowIfNull(inOriginal);

        EncodedTextPositions = inOriginal.EncodedTextPositions;
        EncodedTestsAsBoolList = inOriginal.EncodedTestsAsBoolList;
        EncodedTexts = inOriginal.EncodedTexts;
        _positionsDictionary = inOriginal._positionsDictionary;
    }

    /// <summary>
    ///     The list of string identifiers, and the bit position of the text for the identifier inside the
    ///     <see cref="EncodedTexts" /> byte array or <see cref="EncodedTestsAsBoolList" /> list.
    /// </summary>
    public IList<IdentifierPositionTuple<T>> EncodedTextPositions { get; }

    /// <summary>
    ///     Returns the string as an encoded bool list.
    /// </summary>
    public IList<bool> EncodedTestsAsBoolList { get; }

    /// <summary>
    ///     All the encoded texts as a single byte array. This only exists if the result was created with set encoding!
    ///     <see cref="HuffmanEncoder.GetByteArrayForBoolList(IList{bool}, Endian, bool)" /> to get the wanted byte
    ///     representation for <see cref="EncodedTestsAsBoolList" /> or call <see cref="CreateEncodedTexts(Endian, bool)" />
    /// </summary>
    // CA1819: Properties should not return arrays
#pragma warning disable CA1819
    public byte[]? EncodedTexts { get; internal set; }
#pragma warning restore CA1819

    /// <summary>
    ///     Returns the values of the <see cref="EncodedTextPositions" /> as dictionary for easier lookup outside of loop
    ///     queries.
    /// </summary>
    /// <returns>
    ///     Dictionary with string identifiers and their bit offset in the <see cref="EncodedTexts" /> or
    ///     <see cref="EncodedTestsAsBoolList" />
    /// </returns>
    public Dictionary<T, int> GetTextPositionsDictionary()
    {
        return _positionsDictionary ??=
            EncodedTextPositions.ToDictionary(entry => entry.Identifier, entry => entry.Position);
    }

    /// <summary>
    ///     Creates a new value for <see cref="EncodedTexts" /> based on the given inputs, replacing any previous set one and
    ///     returning the new value.
    ///     <seealso cref="HuffmanEncoder.GetByteArrayForBoolList(IList{bool}, Endian, bool)" />
    /// </summary>
    /// <param name="endian">The endian to use</param>
    /// <param name="usePadding">Whether to pad the byte array.</param>
    /// <returns>The non-null value of <see cref="EncodedTexts" /></returns>
    public byte[] CreateEncodedTexts(Endian endian = Endian.Little, bool usePadding = true)
    {
        EncodedTexts = HuffmanEncoder.GetByteArrayForBoolList(EncodedTestsAsBoolList, endian, usePadding);
        return EncodedTexts;
    }
}