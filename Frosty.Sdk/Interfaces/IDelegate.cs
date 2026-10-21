namespace Frosty.Sdk.Interfaces;

// CA1711: Identifiers should not have incorrect suffix
#pragma warning disable CA1711
public interface IDelegate
#pragma warning restore CA1711
{
    IType? FunctionType { get; set; }
}