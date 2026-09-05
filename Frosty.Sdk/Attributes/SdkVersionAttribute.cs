namespace Frosty.Sdk.Attributes;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class SdkVersionAttribute(uint head) : Attribute
{
    public uint Head { get; } = head;
}