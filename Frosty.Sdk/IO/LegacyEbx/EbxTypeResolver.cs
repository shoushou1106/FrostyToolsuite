namespace Frosty.Sdk.IO.LegacyEbx;

internal sealed class EbxTypeResolver
{
    private readonly IReadOnlyList<EbxFieldDescriptor> _fieldDescriptors;
    private readonly IReadOnlyList<EbxTypeDescriptor> _typeDescriptors;

    internal EbxTypeResolver(IReadOnlyList<EbxTypeDescriptor> inTypeDescriptors,
        IReadOnlyList<EbxFieldDescriptor> inFieldDescriptors)
    {
        EbxSharedTypeDescriptors.Initialize();
        _typeDescriptors = inTypeDescriptors;
        _fieldDescriptors = inFieldDescriptors;
    }

    public EbxTypeDescriptor ResolveType(int index)
    {
        EbxTypeDescriptor typeDescriptor = _typeDescriptors[index];
        return typeDescriptor.IsSharedTypeDescriptorKey()
            ? EbxSharedTypeDescriptors.GetTypeDescriptor(typeDescriptor.ToKey())
            : typeDescriptor;
    }

    public EbxTypeDescriptor ResolveType(EbxTypeDescriptor typeDescriptor, int index)
    {
        return typeDescriptor.Index != -1
            ? EbxSharedTypeDescriptors.GetTypeDescriptor((short)(index + typeDescriptor.Index))
            : _typeDescriptors[index];
    }

    public EbxFieldDescriptor ResolveField(int index)
    {
        return _fieldDescriptors.Count == 0
            ? EbxSharedTypeDescriptors.GetFieldDescriptor(index)
            : _fieldDescriptors[index];
    }
}