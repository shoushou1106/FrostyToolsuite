using System.Buffers.Binary;
using System.Security.Cryptography;

using Frosty.Sdk.Ebx;

namespace Frosty.Sdk.Utils;

/// <summary>
///     Creates ids for new assets.
/// </summary>
/// <remarks>
///     Formerly inside Utils.Utils
/// </remarks>
public static class AssetIdGenerator
{
    public static Guid CreateDeterministicGuid(IEnumerable<object> objects, Guid fileGuid)
    {
        ArgumentNullException.ThrowIfNull(objects);

        int createCount = 0;
        HashSet<Guid> existingGuids = [];
        foreach (dynamic obj in objects)
        {
            AssetClassGuid objGuid = obj.GetInstanceGuid();
            existingGuids.Add(objGuid.ExportedGuid);
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

    public static ulong CreateResourceId()
    {
        // Any random odd value
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer) | 1;
    }
}