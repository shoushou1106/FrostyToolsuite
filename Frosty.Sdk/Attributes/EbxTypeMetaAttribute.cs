using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Attributes;

/// <summary>
///     Mandatory attribute for all Ebx based classes
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Type)]
public sealed class EbxTypeMetaAttribute : Attribute
{
    public EbxTypeMetaAttribute(ushort flags, byte alignment, ushort size)
    {
        Flags = flags;
        Alignment = alignment;
        Size = size;
    }

    public EbxTypeMetaAttribute(
        TypeFlags.TypeEnum typeEnum,
        TypeFlags.CategoryEnum categoryEnum = TypeFlags.CategoryEnum.None)
    {
        Flags = new TypeFlags(typeEnum, categoryEnum);
    }

    public TypeFlags.TypeEnum TypeEnum => Flags.GetTypeEnum();
    public TypeFlags.CategoryEnum CategoryEnum => Flags.GetCategoryEnum();
    public TypeFlags Flags { get; internal set; }
    public byte Alignment { get; internal set; }
    public ushort Size { get; internal set; }
}