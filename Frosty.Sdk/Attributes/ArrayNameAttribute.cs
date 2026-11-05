namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
public sealed class ArrayNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}