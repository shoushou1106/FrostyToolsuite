using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     The ID of an instance in an EBX partition. Usually an exported GUID, an internal index, or both.
/// </summary>
public readonly struct AssetClassGuid : IEquatable<AssetClassGuid>
{
    public AssetClassGuid(Guid exportedGuid, int internalId)
    {
        ExportedGuid = exportedGuid;
        InternalId = internalId;
        IsExported = exportedGuid != Guid.Empty;
    }

    public AssetClassGuid(int internalId)
    {
        ExportedGuid = Guid.Empty;
        InternalId = internalId;
        IsExported = false;
    }

    /// <summary>
    ///     The GUID that other partitions use to reference the instance, or <see cref="Guid.Empty" /> if not exported.
    /// </summary>
    public Guid ExportedGuid { get; }

    /// <summary>
    ///     The index of the instance in its partition, or -1 if not set yet.
    /// </summary>
    public int InternalId { get; }

    public bool IsExported { get; }

    public static bool operator ==(AssetClassGuid a, AssetClassGuid b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(AssetClassGuid a, AssetClassGuid b)
    {
        return !a.Equals(b);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is AssetClassGuid other && Equals(other);
    }

    public bool Equals(AssetClassGuid other)
    {
        return ExportedGuid == other.ExportedGuid
               && InternalId == other.InternalId
               && IsExported == other.IsExported;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ExportedGuid, InternalId, IsExported);
    }

    public override string ToString()
    {
        return IsExported
            ? ExportedGuid.ToString()
            : string.Create(CultureInfo.InvariantCulture, $"00000000-0000-0000-0000-{InternalId:x12}");
    }
}
