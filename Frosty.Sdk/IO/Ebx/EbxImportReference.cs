using System.Globalization;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.Ebx;

/// <summary>
///     A reference to an instance in another partition, ordered by the bytes of the partition GUID and then of the
///     instance GUID.
/// </summary>
public readonly record struct EbxImportReference(Guid PartitionGuid, Guid InstanceGuid)
    : IComparable<EbxImportReference>
{
    public int CompareTo(EbxImportReference other)
    {
        int partitionGuidComparison = PartitionGuid.CompareByBytesTo(other.PartitionGuid);
        return partitionGuidComparison != 0
            ? partitionGuidComparison
            : InstanceGuid.CompareByBytesTo(other.InstanceGuid);
    }

    public static bool operator <(EbxImportReference a, EbxImportReference b)
    {
        return a.CompareTo(b) < 0;
    }

    public static bool operator <=(EbxImportReference a, EbxImportReference b)
    {
        return a.CompareTo(b) <= 0;
    }

    public static bool operator >(EbxImportReference a, EbxImportReference b)
    {
        return a.CompareTo(b) > 0;
    }

    public static bool operator >=(EbxImportReference a, EbxImportReference b)
    {
        return a.CompareTo(b) >= 0;
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{PartitionGuid}/{InstanceGuid}");
    }
}