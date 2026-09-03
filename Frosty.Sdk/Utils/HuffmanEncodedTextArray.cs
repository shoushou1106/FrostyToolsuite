using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

/// <summary>
/// Return value of the encoding function. This contains the encoded texts as byte array, as well as the list of <see cref="IdentifierPositionTuple{T}"/> that detail which text is at what bit offset inside the array.
/// <list type="bullet">
/// <item>
/// <description><c>HuffmanEncodedTextArray.EncodedTexts</c> returns the given strings encoded to a byte array..</description>
/// </item>
/// <item>
/// <description><c>HuffmanEncodedTextArray.EncodedTextPositions</c> returns the text keys an their bit offsets in the <c>EncodedTexts</c> in the form of a list of tuples for use in iterations.</description>
/// </item>
/// <item>
/// <description><c>HuffmanEncodedTextArray.GetTextPositionsDictionary()</c> returns a dictionary with the the same data as above.</description>
/// </item>
/// </list>
/// </summary>
/// <typeparam name="T">The type of identifier used for the texts.</typeparam>
public class HuffmanEncodedTextArray<T> where T : notnull
{
    /// <summary>
    /// The list of string identifiers, and the bit position of the text for the identifier inside the <see cref="EncodedTexts"/> byte array or <see cref="EncodedTestsAsBools"/> list.
    /// </summary>
    public IList<IdentifierPositionTuple<T>> EncodedTextPositions { get; private set; }

    /// <summary>
    /// Returns the string as encoded bools.
    /// </summary>
    public IList<bool> EncodedTestsAsBools { get; private set; }

    /// <summary>
    /// All the encoded texts as single byte array. This only exists if the result was created with set encoding!
    /// <see cref="HuffmanEncoder.GetByteArrayForBoolList(IList{bool}, Endian, bool)"/> to get the wanted byte representation for <see cref="EncodedTestsAsBools"/> or call <see cref="CreateEncodedTexts(Endian, bool)"/>
    /// </summary>
    public byte[]? EncodedTexts { get; internal set; }

    // Dictionary representation of EncodedTextPositions created when first requested.
    private Dictionary<T, int> m_positionsDictionary = null;

    public HuffmanEncodedTextArray(IList<IdentifierPositionTuple<T>> inEncodedTextPositions, IList<bool> inEncodedTestsAsBools)
    {
        EncodedTextPositions = inEncodedTextPositions;
        EncodedTestsAsBools = inEncodedTestsAsBools;
    }

    /// <summary>
    /// Copy Constructor
    /// </summary>
    /// <param name="inOriginal"></param>
    protected HuffmanEncodedTextArray(HuffmanEncodedTextArray<T> inOriginal)
    {
        EncodedTextPositions = inOriginal.EncodedTextPositions;
        EncodedTestsAsBools = inOriginal.EncodedTestsAsBools;
        EncodedTexts = inOriginal.EncodedTexts;
        m_positionsDictionary = inOriginal.m_positionsDictionary;
    }

    /// <summary>
    /// Returns the values of the <see cref="EncodedTextPositions"/> as dictionary for easier lookup outside of loop queries.
    /// </summary>
    /// <returns>Dictionary with string identifiers and their bit offset in the <see cref="EncodedTexts"/> or <see cref="EncodedTestsAsBools"/> </returns>
    public Dictionary<T, int> GetTextPositionsDictionary()
    {
        if (m_positionsDictionary == null)
        {
            m_positionsDictionary = new Dictionary<T, int>(this.EncodedTextPositions.Select(entry => KeyValuePair.Create(entry.Identifier, entry.Position)).ToList());
        }
        return m_positionsDictionary;
    }

    /// <summary>
    /// Creates a new value for <see cref="EncodedTexts"/> based on the given inputs, replacing any previous set one and returning the new value.
    /// <seealso cref="HuffmanEncoder.GetByteArrayForBoolList(IList{bool}, Endian, bool)"/>
    /// </summary>
    /// <param name="endian">The endian to use</param>
    /// <param name="usePadding">Whether or not to padd the byte array.</param>
    /// <returns>The non null value of <see cref="EncodedTexts"/></returns>
    public byte[] CreateEncodedTexts(Endian endian = Endian.Little, bool usePadding = true)
    {
        EncodedTexts = HuffmanEncoder.GetByteArrayForBoolList(EncodedTestsAsBools, endian, usePadding);
        return EncodedTexts;
    }
}