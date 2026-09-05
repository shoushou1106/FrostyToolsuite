namespace Frosty.Sdk.Attributes;

/// <summary>
///     Specifies that this property is read-only
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Field)]
public sealed class IsReadOnlyAttribute : Attribute
{
}