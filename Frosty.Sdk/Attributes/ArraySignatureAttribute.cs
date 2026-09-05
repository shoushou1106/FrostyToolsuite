namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type)]
public sealed class ArraySignatureAttribute(uint signature) : Attribute
{
    public uint Signature { get; } = signature;
}