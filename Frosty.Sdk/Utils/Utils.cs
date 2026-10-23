using System.Buffers.Binary;
using System.Security.Cryptography;

using Frosty.Sdk.Ebx;

namespace Frosty.Sdk.Utils;

// CA1724: Type names should not match namespaces
#pragma warning disable CA1724
public static class Utils
{
    public static string BaseDirectory { get; set; } = string.Empty;

    public static int HashString(string value, bool toLower = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        const uint kOffset = 5381;
        const uint kPrime = 33;

        uint hash = kOffset;
        // Hot path, keep it a plain loop instead of LINQ
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (char c in value)
        {
            hash = (hash * kPrime) ^ (byte)(toLower ? char.ToLowerInvariant(c) : c);
        }

        return (int)hash;
    }

    public static int HashStringA(string value, bool toLower = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        const uint kOffset = 5381;
        const uint kPrime = 33;

        uint hash = kOffset;
        foreach (char c in value)
        {
            hash ^= (byte)(toLower ? char.ToLowerInvariant(c) : c);
            hash *= kPrime;
        }

        return (int)hash;
    }

    public static Guid GenerateDeterministicGuid(IEnumerable<object> objects, Guid fileGuid)
    {
        ArgumentNullException.ThrowIfNull(objects);

        Guid outGuid;

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
        while (true)
        {
            // Generate a deterministic unique guid
            BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], ++createCount);

            // CA5351: Do Not Use Broken Cryptographic Algorithms
#pragma warning disable CA5351
            MD5.HashData(buffer, result);
#pragma warning restore CA5351
            outGuid = new Guid(result);

            if (!existingGuids.Contains(outGuid))
            {
                break;
            }
        }

        return outGuid;
    }

    public static Sha1 GenerateSha1(ReadOnlySpan<byte> buffer)
    {
        Span<byte> hashed = stackalloc byte[20];
        // CA5350: Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5350
        SHA1.HashData(buffer, hashed);
#pragma warning restore CA5350
        Sha1 newSha1 = new(hashed);
        return newSha1;
    }

    public static ulong GenerateResourceId()
    {
        // Any random odd value
        Span<byte> buf = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(buf);
        return BinaryPrimitives.ReadUInt64LittleEndian(buf) | 1;
    }

    public static int CompareToBigEndian(this Guid a, Guid b)
    {
        Span<byte> bytes = stackalloc byte[0x20];

        a.TryWriteBytes(bytes);
        b.TryWriteBytes(bytes[0x10..]);

        for (int i = 0; i < 0x10; i++)
        {
            int c = bytes[i].CompareTo(bytes[i + 0x10]);
            if (c != 0)
            {
                return c;
            }
        }

        return 0;
    }
}
#pragma warning restore CA1724