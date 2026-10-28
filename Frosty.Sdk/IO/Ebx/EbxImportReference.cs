using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.Ebx;

/// <remarks>
///     As a <see langword="record" /> <see langword="struct" />,
///     the compiler generates <see cref="IEquatable{T}" />, <c>==</c>, <c>!=</c> and <see cref="GetHashCode" /> automatically.
/// </remarks>
public readonly record struct EbxImportReference(Guid PartitionGuid, Guid InstanceGuid)
    : IComparable<EbxImportReference>
{
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

    public int CompareTo(EbxImportReference other)
    {
        int partitionGuidComparison = PartitionGuid.CompareToBigEndian(other.PartitionGuid);
        return partitionGuidComparison != 0
            ? partitionGuidComparison
            : InstanceGuid.CompareToBigEndian(other.InstanceGuid);
    }

    public override string ToString()
    {
        return $"{PartitionGuid}/{InstanceGuid}";
    }
}
