namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
public sealed class TypeOffsetAttribute(long offset) : Attribute
{
    public long Offset { get; } = offset;
}