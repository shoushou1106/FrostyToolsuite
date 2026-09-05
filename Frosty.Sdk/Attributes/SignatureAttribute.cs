namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type)]
public sealed class SignatureAttribute(uint signature) : Attribute
{
    public uint Signature { get; } = signature;
}