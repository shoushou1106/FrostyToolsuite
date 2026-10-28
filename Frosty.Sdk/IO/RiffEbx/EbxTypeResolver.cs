namespace Frosty.Sdk.IO.RiffEbx;

internal sealed class EbxTypeResolver
{
    // The writer keeps adding to these lists after the resolver is created
    private readonly IReadOnlyList<Guid> _typeGuids;
    private readonly IReadOnlyList<uint> _typeSignatures;

    internal EbxTypeResolver(IReadOnlyList<Guid> inTypeGuids, IReadOnlyList<uint> inTypeSignatures)
    {
        EbxSharedTypeDescriptors.Initialize();
        _typeGuids = inTypeGuids;
        _typeSignatures = inTypeSignatures;
    }

    public EbxTypeDescriptor ResolveType(int index)
    {
        return EbxSharedTypeDescriptors.GetTypeDescriptor(_typeGuids[index], _typeSignatures[index]);
    }

    public static EbxTypeDescriptor ResolveTypeFromField(ushort inTypeDescriptorRef)
    {
        return EbxSharedTypeDescriptors.GetTypeDescriptor(inTypeDescriptorRef);
    }

    public static EbxFieldDescriptor ResolveField(int index)
    {
        return EbxSharedTypeDescriptors.GetFieldDescriptors(index);
    }
}