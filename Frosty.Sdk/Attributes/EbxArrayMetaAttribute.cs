using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type | FrostyAttributeTargets.Field)]
public sealed class EbxArrayMetaAttribute(ushort flags) : Attribute
{
    public TypeFlags Flags { get; internal set; } = flags;
}