using System.Buffers.Binary;
using System.Security.Cryptography;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     Creates IDs for new assets.
/// </summary>
public static class AssetIdGenerator
{
    /// <summary>
    ///     Creates an instance GUID that is the same for the same file and instance count,
    ///     and not used by <paramref name="objects" />.
    /// </summary>
    /// <param name="objects">The instances already in the file.</param>
    /// <param name="fileGuid">The GUID of the file.</param>
    public static Guid CreateDeterministicGuid(IEnumerable<IEbxInstance> objects, Guid fileGuid)
    {
        ArgumentNullException.ThrowIfNull(objects);

        int createCount = 0;
        HashSet<Guid> existingGuids = [];
        foreach (IEbxInstance obj in objects)
        {
            existingGuids.Add(obj.GetInstanceGuid().ExportedGuid);
            createCount++;
        }

        Span<byte> buffer = stackalloc byte[20];
        fileGuid.TryWriteBytes(buffer);

        Span<byte> result = stackalloc byte[16];
        Guid outGuid;
        do
        {
            // Generate a deterministic unique guid
            BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], ++createCount);

            // CA5351: Do Not Use Broken Cryptographic Algorithms
#pragma warning disable CA5351
            MD5.HashData(buffer, result);
#pragma warning restore CA5351
            outGuid = new Guid(result);
        } while (existingGuids.Contains(outGuid));

        return outGuid;
    }

    /// <summary>
    ///     Creates a random resource id, always odd.
    /// </summary>
    public static ulong CreateResourceId()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer) | 1;
    }
}