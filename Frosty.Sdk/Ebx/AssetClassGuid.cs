using System.Diagnostics.CodeAnalysis;

namespace Frosty.Sdk.Ebx;

public readonly struct AssetClassGuid : IEquatable<AssetClassGuid>
{
    public Guid ExportedGuid { get; }
    public int InternalId { get; }
    public bool IsExported { get; }

    public AssetClassGuid(Guid inGuid, int inId)
    {
        ExportedGuid = inGuid;
        InternalId = inId;
        IsExported = inGuid != Guid.Empty;
    }

    public AssetClassGuid(int inId)
    {
        ExportedGuid = Guid.Empty;
        InternalId = inId;
        IsExported = false;
    }

    public static bool operator ==(AssetClassGuid a, object? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(AssetClassGuid a, object? b)
    {
        return !a.Equals(b);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj switch
        {
            AssetClassGuid reference => Equals(reference),
            Guid guid => IsExported && guid == ExportedGuid,
            int id => InternalId == id,
            _ => false
        };
    }

    public bool Equals(AssetClassGuid other)
    {
        return ExportedGuid.Equals(other.ExportedGuid) && InternalId == other.InternalId &&
               IsExported == other.IsExported;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)2166136261;
            hash = (hash * 16777619) ^ ExportedGuid.GetHashCode();
            hash = (hash * 16777619) ^ InternalId.GetHashCode();
            hash = (hash * 16777619) ^ IsExported.GetHashCode();
            return hash;
        }
    }

    public override string ToString()
    {
        return IsExported ? ExportedGuid.ToString() : $"00000000-0000-0000-0000-{InternalId:x12}";
    }
}