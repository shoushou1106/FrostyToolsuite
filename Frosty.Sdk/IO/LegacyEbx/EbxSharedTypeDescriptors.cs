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
        using BlockAccessor accessor = new(file, true);
        EbxVersion magic = (EbxVersion)accessor.ReadUInt32();

        if (magic != EbxVersion.Version4)
        {
            throw new InvalidDataException("magic");
        }

        ushort typeDescriptorCount = accessor.ReadUInt16();
        ushort fieldDescriptorCount = accessor.ReadUInt16();

        TypeDescriptors.Capacity = typeDescriptorCount + TypeDescriptors.Count;
        FieldDescriptors.Capacity = fieldDescriptorCount + FieldDescriptors.Count;

        int startFields = FieldDescriptors.Count;

        for (int i = 0; i < fieldDescriptorCount; i++)
        {
            FieldDescriptors.Add(new EbxFieldDescriptor
            {
                NameHash = accessor.ReadUInt32(),
                Flags = accessor.ReadUInt16(),
                TypeDescriptorRef = accessor.ReadUInt16(),
                DataOffset = accessor.ReadUInt32(),
                SecondOffset = accessor.ReadUInt32()
            });
        }

        for (int i = 0; i < typeDescriptorCount; i++)
        {
            long offset = accessor.Position;

            Guid key = accessor.ReadGuid();

            EbxTypeDescriptor typeDescriptor = new()
            {
                NameHash = accessor.ReadUInt32(),
                FieldIndex = accessor.ReadInt32(),
                FieldCount = accessor.ReadByte(),
                Alignment = accessor.ReadByte(),
                Flags = accessor.ReadUInt16(),
                Size = accessor.ReadUInt16(),
                SecondSize = accessor.ReadUInt16(),
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
                typeDescriptor.FieldIndex = (int)(((offset - (typeDescriptor.FieldIndex - 0x08)) / 0x10) + startFields);
                KeyTypeMapping[key] = TypeDescriptors.Count;
                TypeKeyMapping[typeDescriptor.NameHash] = key;
            }

            TypeDescriptors.Add(typeDescriptor);
        }
    }
}