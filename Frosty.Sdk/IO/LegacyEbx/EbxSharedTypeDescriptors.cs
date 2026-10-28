using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.LegacyEbx;

internal static class EbxSharedTypeDescriptors
{
    private static bool s_isInitialized;

    private static readonly Dictionary<uint, Guid> TypeKeyMapping = new();
    private static readonly Dictionary<Guid, int> KeyTypeMapping = new();
    private static readonly List<EbxFieldDescriptor> FieldDescriptors = [];
    private static readonly List<EbxTypeDescriptor> TypeDescriptors = [];

    public static void Initialize()
    {
        if (s_isInitialized)
        {
            return;
        }

        if (FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors.ebx"))
        {
            Read(FileSystemManager.GetFileFromMemoryFs("SharedTypeDescriptors.ebx"));
        }

        if (FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors_patch.ebx"))
        {
            Read(FileSystemManager.GetFileFromMemoryFs("SharedTypeDescriptors_patch.ebx"));
        }

        s_isInitialized = true;
    }

    public static EbxTypeDescriptor GetTypeDescriptor(Guid key)
    {
        return TypeDescriptors[KeyTypeMapping[key]];
    }

    public static EbxTypeDescriptor GetTypeDescriptor(short index)
    {
        return TypeDescriptors[index];
    }

    public static EbxFieldDescriptor GetFieldDescriptor(int index)
    {
        return FieldDescriptors[index];
    }

    public static EbxTypeDescriptor GetKey(uint inNameHash)
    {
        return new EbxTypeDescriptor(TypeKeyMapping[inNameHash]);
    }

    public static EbxTypeDescriptor GetSharedTypeDescriptor(uint inNameHash)
    {
        return TypeDescriptors[KeyTypeMapping[TypeKeyMapping[inNameHash]]];
    }

    private static void Read(Block<byte> file)
    {
        // Left open because the block belongs to the memory fs
        using BlockStream stream = new(file, true);
        EbxVersion magic = (EbxVersion)stream.ReadUInt32();

        if (magic != EbxVersion.Version4)
        {
            throw new InvalidDataException("magic");
        }

        ushort typeDescriptorCount = stream.ReadUInt16();
        ushort fieldDescriptorCount = stream.ReadUInt16();

        TypeDescriptors.Capacity = typeDescriptorCount + TypeDescriptors.Count;
        FieldDescriptors.Capacity = fieldDescriptorCount + FieldDescriptors.Count;

        int startFields = FieldDescriptors.Count;

        for (int i = 0; i < fieldDescriptorCount; i++)
        {
            FieldDescriptors.Add(new EbxFieldDescriptor
            {
                NameHash = stream.ReadUInt32(),
                Flags = stream.ReadUInt16(),
                TypeDescriptorRef = stream.ReadUInt16(),
                DataOffset = stream.ReadUInt32(),
                SecondOffset = stream.ReadUInt32()
            });
        }

        for (int i = 0; i < typeDescriptorCount; i++)
        {
            long offset = stream.Position;

            Guid key = stream.ReadGuid();

            EbxTypeDescriptor typeDescriptor = new()
            {
                NameHash = stream.ReadUInt32(),
                FieldIndex = stream.ReadInt32(),
                FieldCount = stream.ReadByte(),
                Alignment = stream.ReadByte(),
                Flags = stream.ReadUInt16(),
                Size = stream.ReadUInt16(),
                SecondSize = stream.ReadUInt16(),
                Index = TypeDescriptors.Count
            };

            typeDescriptor.Name = TypeLibrary.GetType(typeDescriptor.NameHash)?.Name ?? string.Empty;

            if (typeDescriptor.IsSharedTypeDescriptorKey())
            {
                // Reference to already existing type descriptor
                typeDescriptor = TypeDescriptors[KeyTypeMapping[key]];
            }
            else
            {
                // Relative offset to the field, calculate the index.
                typeDescriptor.FieldIndex = (int)((offset - (typeDescriptor.FieldIndex - 0x08)) / 0x10 + startFields);
                KeyTypeMapping[key] = TypeDescriptors.Count;
                TypeKeyMapping[typeDescriptor.NameHash] = key;
            }

            TypeDescriptors.Add(typeDescriptor);
        }
    }
}