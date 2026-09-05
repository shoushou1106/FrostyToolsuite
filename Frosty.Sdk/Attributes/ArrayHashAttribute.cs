namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
public sealed class ArrayHashAttribute(uint hash) : Attribute
{
    public uint Hash { get; internal set; } = hash;
}