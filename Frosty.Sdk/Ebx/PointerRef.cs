using System.Diagnostics.CodeAnalysis;

using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Ebx;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     A value of an EBX field that points to an instance in the same partition (internal) or in another one (external).
/// </summary>
public readonly struct PointerRef : IEquatable<PointerRef>
{
    public EbxImportReference External { get; }
    public IEbxInstance? Internal { get; }
    public PointerRefType Type { get; }

    public PointerRef()
    {
        External = new EbxImportReference();
        Internal = null;
        Type = PointerRefType.Null;
    }

    public PointerRef(EbxImportReference externalRef)
    {
        External = externalRef;
        Internal = null;
        Type = PointerRefType.External;
    }

    public PointerRef(Guid partitionGuid)
    {
        External = new EbxImportReference { PartitionGuid = partitionGuid, InstanceGuid = Guid.Empty };
        Internal = null;
        Type = partitionGuid != Guid.Empty ? PointerRefType.External : PointerRefType.Null;
    }

    public PointerRef(IEbxInstance internalRef)
    {
        External = new EbxImportReference();
        Internal = internalRef;
        Type = PointerRefType.Internal;
    }

    public static bool operator ==(PointerRef a, PointerRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(PointerRef a, PointerRef b)
    {
        return !a.Equals(b);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is PointerRef other && Equals(other);
    }

    public bool Equals(PointerRef other)
    {
        return Type == other.Type && Internal == other.Internal && External == other.External;
    }

    public override int GetHashCode()
    {
        return Type switch
        {
            PointerRefType.Internal => HashCode.Combine(Type, Internal),
            PointerRefType.External => HashCode.Combine(Type, External),
            _ => HashCode.Combine(Type)
        };
    }
}