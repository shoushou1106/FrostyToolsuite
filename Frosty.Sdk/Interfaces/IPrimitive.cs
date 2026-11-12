namespace Frosty.Sdk.Interfaces;

/// <summary>
///     A wrapper of an EBX primitive value, implemented by the generated SDK types.
/// </summary>
/// <example><c>Int32</c>, <c>CString</c></example>
public interface IPrimitive
{
    /// <summary>
    ///     Gets the wrapped .NET value.
    /// </summary>
    /// <example><see cref="int" />, <see cref="string" /></example>
    object ToActualType();

    /// <summary>
    ///     Sets the wrapped value from a .NET value of the matching type.
    /// </summary>
    void FromActualType(object value);
}
