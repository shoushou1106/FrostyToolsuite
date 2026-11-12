namespace Frosty.Sdk.Interfaces;

/// <summary>
///     A value of an EBX delegate field, implemented by the generated SDK types.
/// </summary>
// CA1711: Identifiers should not have incorrect suffix
#pragma warning disable CA1711
public interface IDelegate
#pragma warning restore CA1711
{
    /// <summary>
    ///     The function type that the delegate points to, or <see langword="null" /> if not set.
    /// </summary>
    IType? FunctionType { get; set; }
}