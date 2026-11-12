using System.Buffers.Binary;
using System.Security.Cryptography;

using Frosty.Sdk.Exceptions;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Profiles;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

/// <summary>
///     The asset list of a bundle in the binary format (EBX, RES and Chunk entries).
/// </summary>
public sealed class BinaryBundle
{
    // CA1008: Enums should have zero value
    // CA1028: Enum storage should be Int32
#pragma warning disable CA1008, CA1028
    public enum Magic : uint
#pragma warning restore CA1008, CA1028
    {
        Standard = 0xED1CEDB8,
        Kelvin = 0xC3889333,
        Encrypted = 0xC3E5D5C3
    }

    private BinaryBundle(DataAccessor accessor)
    {
        // The header is the magic and 7 more fields
        const int headerSize = 0x20;

        uint size = accessor.ReadUInt32(Endian.Big);

        long startPos = accessor.Position;

        uint salt = GetSalt();
        uint rawMagic = accessor.ReadUInt32(Endian.Big);
        Endian endian = Endian.Big;
        Magic magic = (Magic)(rawMagic ^ salt);

        if (!IsValidMagic(magic))
        {
            endian = Endian.Little;
            magic = (Magic)(BinaryPrimitives.ReverseEndianness(rawMagic) ^ salt);

            if (!IsValidMagic(magic))
            {
                throw new InvalidDataException($"The bundle magic 0x{rawMagic:X8} is not valid.");
            }
        }

        bool containsSha1 = magic == Magic.Standard;

        uint totalCount = accessor.ReadUInt32(endian);
        int ebxCount = accessor.ReadInt32(endian);
        int resCount = accessor.ReadInt32(endian);
        int chunkCount = accessor.ReadInt32(endian);
        uint stringsOffsetInBundle = accessor.ReadUInt32(endian);
        accessor.Position += sizeof(uint) + sizeof(int); // metaOffset + metaSize

        EbxAssetEntry[] ebxList = new EbxAssetEntry[ebxCount];
        ResAssetEntry[] resList = new ResAssetEntry[resCount];
        ChunkAssetEntry[] chunkList = new ChunkAssetEntry[chunkCount];

        Sha1[] sha1 = new Sha1[totalCount];

        DataAccessor bundle = accessor;
        long stringsOffset = startPos + stringsOffsetInBundle;
        using DataAccessor? decrypted =
            magic == Magic.Encrypted ?
                Decrypt(accessor, (int)size - headerSize):
                null;
        if (decrypted is not null)
        {
            // The decrypted data starts after the header
            bundle = decrypted;
            stringsOffset = stringsOffsetInBundle - headerSize;
        }

        // Read SHA-1s
        for (int i = 0; i < totalCount; i++)
        {
            sha1[i] = containsSha1 ? bundle.ReadSha1() : Sha1.Zero;
        }

        int j = 0;
        for (int i = 0; i < ebxCount; i++, j++)
        {
            uint nameOffset = bundle.ReadUInt32(endian);
            uint originalSize = bundle.ReadUInt32(endian);

            long currentPos = bundle.Position;
            bundle.Position = stringsOffset + nameOffset;
            string name = bundle.ReadNullTerminatedString();

            ebxList[i] = new EbxAssetEntry(name, sha1[j], originalSize);

            bundle.Position = currentPos;
        }

        long resTypeOffset = bundle.Position + (resCount * 2 * sizeof(uint));
        long resMetaOffset = bundle.Position + (resCount * 2 * sizeof(uint)) + (resCount * sizeof(uint));
        long resRidOffset = bundle.Position + (resCount * 2 * sizeof(uint)) + (resCount * sizeof(uint)) +
                            (resCount * 0x10);
        for (int i = 0; i < resCount; i++, j++)
        {
            uint nameOffset = bundle.ReadUInt32(endian);
            uint originalSize = bundle.ReadUInt32(endian);

            long currentPos = bundle.Position;
            bundle.Position = stringsOffset + nameOffset;
            string name = bundle.ReadNullTerminatedString();

            bundle.Position = resTypeOffset + (i * sizeof(uint));
            uint resType = bundle.ReadUInt32(endian);

            bundle.Position = resMetaOffset + (i * 0x10);
            byte[] resMeta = bundle.ReadBytes(0x10);

            bundle.Position = resRidOffset + (i * sizeof(ulong));
            ulong resRid = bundle.ReadUInt64(endian);

            resList[i] = new ResAssetEntry(name, sha1[j], originalSize, resRid, resType, resMeta);

            bundle.Position = currentPos;
        }

        bundle.Position = resRidOffset + (resCount * sizeof(ulong));
        for (int i = 0; i < chunkCount; i++, j++)
        {
            chunkList[i] = new ChunkAssetEntry(bundle.ReadGuid(endian), sha1[j], bundle.ReadUInt32(endian),
                bundle.ReadUInt32(endian));
        }

        EbxList = ebxList;
        ResList = resList;
        ChunkList = chunkList;

        // Set the correct position because the string table comes after the meta
        accessor.Position = startPos + size;
    }

    public IReadOnlyList<EbxAssetEntry> EbxList { get; }
    public IReadOnlyList<ResAssetEntry> ResList { get; }
    public IReadOnlyList<ChunkAssetEntry> ChunkList { get; }

    /// <summary>
    ///     Reads <paramref name="size" /> bytes and decrypts them with the bundle encryption key.
    /// </summary>
    /// <exception cref="MissingEncryptionKeyException">The bundle encryption key is not loaded.</exception>
    private static DataAccessor Decrypt(DataAccessor accessor, int size)
    {
        if (!KeyManager.HasKey("BundleEncryptionKey"))
        {
            throw MissingEncryptionKeyException.ForKey("BundleEncryptionKey");
        }

        byte[] key = KeyManager.GetKey("BundleEncryptionKey");
        byte[] data = accessor.ReadBytes(size);

        using Aes aes = Aes.Create();
        aes.Key = key;
        // The key is also the IV
        aes.DecryptCbc(data, iv: key, data, PaddingMode.None);

        return new DataAccessor(new MemoryStream(data, false));
    }

    /// <summary>
    ///     Only the games using the <see cref="BundleFormat.Kelvin" /> format have their own magic, the rest uses
    ///     <see cref="Magic.Standard" />.
    /// </summary>
    /// <returns>The magic the current game uses.</returns>
    public static Magic CurrentMagic =>
        FileSystemManager.BundleFormat == BundleFormat.Kelvin ? Magic.Kelvin : Magic.Standard;

    /// <summary>
    ///     Gets the salt the magic is XORed with.
    /// </summary>
    /// <remarks>
    ///     "pecn" for >= Frostbite 2017, other use "pecm".
    ///     <see cref="ProfileVersion.Battlefield5" /> is the only game that uses "arie".
    /// </remarks>
    /// <returns>The salt that the current game uses.</returns>
    public static uint GetSalt()
    {
        const uint pecm = 0x7065636D;
        const uint pecn = 0x7065636E;
        const uint arie = 0x61726965;

        if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield5))
        {
            return arie;
        }

        return ProfilesLibrary.FrostbiteVersion >= "2017" ? pecn : pecm;
    }

    public static bool IsValidMagic(Magic magic)
    {
        return magic is Magic.Standard or Magic.Kelvin or Magic.Encrypted;
    }

    /// <summary>
    ///     Reads a bundle in the binary format, starting at its big-endian size.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (asset names)</remarks>
    /// <param name="accessor">The data, left at the end of the bundle.</param>
    /// <exception cref="InvalidDataException">The bundle does not have valid <see cref="Magic" />.</exception>
    /// <exception cref="MissingEncryptionKeyException">The bundle is encrypted and the key is not loaded.</exception>
    public static BinaryBundle Deserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        return new BinaryBundle(accessor);
    }
}