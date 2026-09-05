using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Attributes;

/// <summary>
///     Mandatory attribute for all Ebx based fields
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Field)]
public sealed class EbxFieldMetaAttribute : Attribute
{
    public EbxFieldMetaAttribute(ushort flags, uint offset, Type? baseType)
    {
        Flags = flags;
        Offset = offset;
        BaseType = baseType;
    }

    public EbxFieldMetaAttribute(TypeFlags.TypeEnum typeEnum, uint offset = 0, string baseType = "")
    {
        if (!string.IsNullOrEmpty(baseType))
        {
            BaseType = TypeLibrary.GetType(baseType)?.Type;
        }

        Flags = new TypeFlags(typeEnum);
        Offset = offset;
    }

    public TypeFlags.TypeEnum TypeEnum => Flags.GetTypeEnum();
    public TypeFlags Flags { get; internal set; }
    public uint Offset { get; internal set; }
    public Type? BaseType { get; internal set; }
}