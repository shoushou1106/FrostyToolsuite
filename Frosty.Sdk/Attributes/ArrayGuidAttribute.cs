namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
#pragma warning disable CA1019
public sealed class ArrayGuidAttribute(string inGuid) : Attribute
#pragma warning restore CA1019
{
    public Guid Guid { get; } = Guid.Parse(inGuid);
}