namespace Frosty.Sdk.Utils;

/// <summary>
///     Just a tuple of an identifier and a position with clearer naming than a Tuple.
///     This is used as part of the return value of the encoding method. The identifier is to be the identifier of a string
///     or text, with the coupled position being the bit offset in the encoded byte array.
/// </summary>
/// <typeparam name="T">The type of identifier used, might a simple uint or a complex type.</typeparam>
public class IdentifierPositionTuple<T>(T inIdentifier, int inPosition)
{
    /// <summary>
    ///     The identifier of an encoded string.
    /// </summary>
    public T Identifier { get; } = inIdentifier;

    /// <summary>
    ///     The position of the encoded string.
    /// </summary>
    public int Position { get; } = inPosition;
}