namespace Frosty.Sdk.IO.RiffEbx;

internal sealed class EbxTypeResolver
{
    // The writer keeps adding to these lists after the resolver is created
    private readonly IReadOnlyList<Guid> _typeGuids;
    private readonly IReadOnlyList<uint> _typeSignatures;

    internal EbxTypeResolver(IReadOnlyList<Guid> typeGuids, IReadOnlyList<uint> typeSignatures)
    {
        EbxSharedTypeDescriptors.Initialize();
        _typeGuids = typeGuids;
        _typeSignatures = typeSignatures;
    }

    public EbxTypeDescriptor ResolveType(int index)
    {
        return EbxSharedTypeDescriptors.GetTypeDescriptor(_typeGuids[index], _typeSignatures[index]);
    }

    public static EbxTypeDescriptor ResolveTypeFromField(ushort typeDescriptorRef)
    {
        return EbxSharedTypeDescriptors.GetTypeDescriptor(typeDescriptorRef);
    }

    public static EbxFieldDescriptor ResolveField(int index)
    {
        return EbxSharedTypeDescriptors.GetFieldDescriptors(index);
    }
}