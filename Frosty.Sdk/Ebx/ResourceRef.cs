using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     A value of an EBX field that holds the resource ID (RID) of a RES asset.
/// </summary>
public readonly struct ResourceRef(ulong value) : IEquatable<ResourceRef>
{
    public static readonly ResourceRef Zero = new(0);

    private readonly ulong _resourceId = value;

    public static implicit operator ulong(ResourceRef value)
    {
        return value._resourceId;
    }

    public static implicit operator ResourceRef(ulong value)
    {
        return new ResourceRef(value);
    }

    public static ResourceRef FromUInt64(ulong value)
    {
        return new ResourceRef(value);
    }

    public ulong ToUInt64()
    {
        return _resourceId;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is ResourceRef other && Equals(other);
    }

    public bool Equals(ResourceRef other)
    {
        return _resourceId == other._resourceId;
    }

    public static bool operator ==(ResourceRef a, ResourceRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(ResourceRef a, ResourceRef b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return _resourceId.GetHashCode();
    }

    public override string ToString()
    {
        return _resourceId.ToString("X16", CultureInfo.InvariantCulture);
    }
}