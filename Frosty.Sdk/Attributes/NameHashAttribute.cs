namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type | FrostyAttributeTargets.Field, Inherited = false)]
public sealed class NameHashAttribute(uint hash) : Attribute
{
    public uint Hash { get; } = hash;
}