namespace Frosty.Sdk.Attributes;

/// <summary>
///     Specifies the field's index, which may differ from its offset
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Field)]
public sealed class FieldIndexAttribute(int index) : Attribute
{
    public int Index { get; internal set; } = index;
}