using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.IO;

public abstract class BaseEbxReader(DataStream inStream)
{
    private protected readonly DataStream _stream = inStream;
    private protected readonly List<IEbxInstance?> _instances = [];
    private protected readonly List<int> _refCounts = [];

    #region -- PrimitiveTypes --

    private protected static readonly Type VoidType = TypeLibrary.GetType("Void")!.Type;
    private protected static readonly Type StringType = TypeLibrary.GetType("String")!.Type;
    private protected static readonly Type SByteType = TypeLibrary.GetType("Int8")!.Type;
    private protected static readonly Type ByteType = TypeLibrary.GetType("Uint8")?.Type ?? TypeLibrary.GetType("UInt8")!.Type;
    private protected static readonly Type BoolType = TypeLibrary.GetType("Boolean")!.Type;
    private protected static readonly Type UShortType = TypeLibrary.GetType("Uint16")?.Type ?? TypeLibrary.GetType("UInt16")!.Type;
    private protected static readonly Type ShortType = TypeLibrary.GetType("Int16")!.Type;
    private protected static readonly Type UIntType = TypeLibrary.GetType("Uint32")?.Type ?? TypeLibrary.GetType("UInt32")!.Type;
    private protected static readonly Type IntType = TypeLibrary.GetType("Int32")!.Type;
    private protected static readonly Type ULongType = TypeLibrary.GetType("Uint64")?.Type ?? TypeLibrary.GetType("UInt64")!.Type;
    private protected static readonly Type LongType = TypeLibrary.GetType("Int64")!.Type;
    private protected static readonly Type FloatType = TypeLibrary.GetType("Float32")!.Type;
    private protected static readonly Type DoubleType = TypeLibrary.GetType("Float64")!.Type;
    private protected static readonly Type PointerType = typeof(PointerRef);
    private protected static readonly Type GuidType = TypeLibrary.GetType("Guid")!.Type;
    private protected static readonly Type Sha1Type = TypeLibrary.GetType("SHA1")!.Type;
    private protected static readonly Type CStringType = TypeLibrary.GetType("CString")!.Type;
    private protected static readonly Type ResourceRefType = TypeLibrary.GetType("ResourceRef")!.Type;
    private protected static readonly Type FileRefType = TypeLibrary.GetType("FileRef")!.Type;
    private protected static readonly Type? TypeRefType = TypeLibrary.GetType("TypeRef")?.Type;
    private protected static readonly Type? BoxedValueRefType = TypeLibrary.GetType("BoxedValueRef")?.Type;

    #endregion

    public static BaseEbxReader CreateReader(DataStream inStream)
    {
        return ProfilesLibrary.EbxVersion == 6 ? new RiffEbx.EbxReader(inStream) : new LegacyEbx.EbxReader(inStream);
    }

    public T ReadPartition<T>() where T : EbxPartition, new()
    {
        T asset = new();
        InternalReadObjects();

        for (int i = _instances.Count - 1; i >= 0 ; i--)
        {
            if (_instances[i] is not null)
            {
                continue;
            }

            // Instance type is not in type info
            _instances.RemoveAt(i);
            _refCounts.RemoveAt(i);
        }

        asset.PartitionGuid = GetPartitionGuid();
        asset.InternalInstances = _instances!;
        asset.InternalRefCounts = _refCounts;
        asset.InternalDependencies = GetDependencies();
        asset.OnLoadComplete();

        return asset;
    }

    /// <returns><see langword="false" /> when there is nothing to store.</returns>
    private protected static bool TryWrapValue(Type inTargetType, ref object? value)
    {
        if (typeof(IDelegate).IsAssignableFrom(inTargetType))
        {
            // A delegate without a function type is still stored
            IDelegate @delegate = (IDelegate)Activator.CreateInstance(inTargetType)!;
            @delegate.FunctionType = (IType?)value;
            value = @delegate;
            return true;
        }

        if (value is null)
        {
            return false;
        }

        if (!typeof(IPrimitive).IsAssignableFrom(inTargetType))
        {
            return true;
        }

        IPrimitive primitive = (IPrimitive)Activator.CreateInstance(inTargetType)!;
        primitive.FromActualType(value);
        value = primitive;
        return true;
    }

    public abstract Guid GetPartitionGuid();

    public abstract string GetRootType();

    public abstract HashSet<Guid> GetDependencies();

    protected abstract void InternalReadObjects();
}