using System.Buffers.Binary;
using System.Security.Cryptography;
using Frosty.Sdk.Exceptions;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Profiles;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

public class BinaryBundle
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

    public IReadOnlyList<EbxAssetEntry> EbxList { get; }
    public IReadOnlyList<ResAssetEntry> ResList { get; }
    public IReadOnlyList<ChunkAssetEntry> ChunkList { get; }

    private BinaryBundle(DataStream stream)
    {
        // Use big endian for default
        Endian endian = Endian.Big;

        uint size = stream.ReadUInt32(Endian.Big);

        long startPos = stream.Position;

        Magic magic = (Magic)(stream.ReadUInt32(endian) ^ GetSalt());

        // Check which endian is written
        if (!IsValidMagic(magic))
        {
            endian = Endian.Little;
            uint value = (uint)magic ^ GetSalt();
            magic = (Magic)(BinaryPrimitives.ReverseEndianness(value) ^ GetSalt());

            if (!IsValidMagic(magic))
            {
                throw new InvalidDataException("magic");
            }
        }

        bool containsSha1 = magic == Magic.Standard;

        uint totalCount = stream.ReadUInt32(endian);
        int ebxCount = stream.ReadInt32(endian);
        int resCount = stream.ReadInt32(endian);
        int chunkCount = stream.ReadInt32(endian);
        long stringsOffset = stream.ReadUInt32(endian) + startPos;
        stream.Position += sizeof(uint) + sizeof(int); // metaOffset + metaSize

        EbxAssetEntry[] ebxList = new EbxAssetEntry[ebxCount];
        ResAssetEntry[] resList = new ResAssetEntry[resCount];
        ChunkAssetEntry[] chunkList = new ChunkAssetEntry[chunkCount];

        Sha1[] sha1 = new Sha1[totalCount];

        // Decrypt the data
        if (magic == Magic.Encrypted)
        {
            if (stream is not BlockStream blockStream)
            {
                throw new ArgumentException("Encrypted bundle. The bundle needs to be passed in as a BlockStream.",
                    nameof(stream));
            }

            if (!KeyManager.HasKey("BundleEncryptionKey"))
            {
                throw new MissingEncryptionKeyException("bundles");
            }

            blockStream.Decrypt(KeyManager.GetKey("BundleEncryptionKey"), (int)(size - 0x20), PaddingMode.None);
        }

        // Read SHA-1s
        for (int i = 0; i < totalCount; i++)
        {
            sha1[i] = containsSha1 ? stream.ReadSha1() : Sha1.Zero;
        }

        int j = 0;
        for (int i = 0; i < ebxCount; i++, j++)
        {
            uint nameOffset = stream.ReadUInt32(endian);
            uint originalSize = stream.ReadUInt32(endian);

            long currentPos = stream.Position;
            stream.Position = stringsOffset + nameOffset;
            string name = stream.ReadNullTerminatedString();

            ebxList[i] = new EbxAssetEntry(name, sha1[j], originalSize);

            stream.Position = currentPos;
        }

        long resTypeOffset = stream.Position + resCount * 2 * sizeof(uint);
        long resMetaOffset = stream.Position + resCount * 2 * sizeof(uint) + resCount * sizeof(uint);
        long resRidOffset = stream.Position + resCount * 2 * sizeof(uint) + resCount * sizeof(uint) + resCount * 0x10;
        for (int i = 0; i < resCount; i++, j++)
        {
            uint nameOffset = stream.ReadUInt32(endian);
            uint originalSize = stream.ReadUInt32(endian);

            long currentPos = stream.Position;
            stream.Position = stringsOffset + nameOffset;
            string name = stream.ReadNullTerminatedString();

            stream.Position = resTypeOffset + i * sizeof(uint);
            uint resType = stream.ReadUInt32(endian);

            stream.Position = resMetaOffset + i * 0x10;
            byte[] resMeta = stream.ReadBytes(0x10);

            stream.Position = resRidOffset + i * sizeof(ulong);
            ulong resRid = stream.ReadUInt64(endian);

            resList[i] = new ResAssetEntry(name, sha1[j], originalSize, resRid, resType, resMeta);

            stream.Position = currentPos;
        }

        stream.Position = resRidOffset + resCount * sizeof(ulong);
        for (int i = 0; i < chunkCount; i++, j++)
        {
            chunkList[i] = new ChunkAssetEntry(stream.ReadGuid(endian), sha1[j], stream.ReadUInt32(endian),
                stream.ReadUInt32(endian));
        }

        EbxList = ebxList;
        ResList = resList;
        ChunkList = chunkList;

        // Set the correct position, because the string table comes after the meta
        stream.Position = startPos + size;
    }

    /// <summary>
    /// Dependent on the FB version, games use different salts.
    /// If the game uses a version newer than 2017 it uses "pecn", else it uses "pecm".
    /// <see cref="ProfileVersion.Battlefield5"/> is the only game that uses "arie".
    /// </summary>
    /// <returns>The salt, that the current game uses.</returns>
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

    /// <summary>
    /// Only the games using the <see cref="BundleFormat.Kelvin"/> format have their own magic, the rest uses <see cref="Magic.Standard"/>.
    /// </summary>
    /// <returns>The magic the current game uses.</returns>
    public static Magic CurrentMagic =>
        FileSystemManager.BundleFormat == BundleFormat.Kelvin ? Magic.Kelvin : Magic.Standard;

    public static bool IsValidMagic(Magic magic) =>
        magic is Magic.Standard or Magic.Kelvin or Magic.Encrypted;

    /// <summary>
    /// Deserialize a binary stored bundle as <see cref="DbObject"/>.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream"/> from which the bundle should be Deserialized from.</param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException">Is thrown when there is no valid <see cref="Magic"/>.</exception>
    public static BinaryBundle Deserialize(DataStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new BinaryBundle(stream);
    }
}