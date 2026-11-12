using System.Diagnostics;

using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.RiffEbx;

internal static class EbxSharedTypeDescriptors
{
    private static bool s_isInitialized;

    private static readonly Dictionary<(Guid, uint), int> Mapping = [];
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

    public static EbxTypeDescriptor GetTypeDescriptor(Guid guid, uint signature)
    {
        return TypeDescriptors[Mapping[(guid, signature)]];
    }

    public static EbxTypeDescriptor GetTypeDescriptor(ushort index)
    {
        return TypeDescriptors[index];
    }

    public static EbxFieldDescriptor GetFieldDescriptors(int index)
    {
        return FieldDescriptors[index];
    }

    private static void Read(Block<byte> file)
    {
        using DataAccessor accessor = new(file.ToStream());
        RiffReader riffReader = new(accessor);
        riffReader.ReadHeader(out FourCc fourCc, out uint size);

        if (fourCc != "EBXT")
        {
            throw new InvalidDataException("Not a valid EBXT chunk");
        }

        // Read REFL chunk
        riffReader.ReadNextChunk(ref size, ProcessChunk);

        Debug.Assert(riffReader.Eof);
    }

    private static void ProcessChunk(DataAccessor accessor, FourCc fourCc, uint size)
    {
        if (fourCc != "REFL" && fourCc != "RFL2")
        {
            return;
        }

        int startFields = FieldDescriptors.Count;
        int startTypes = TypeDescriptors.Count;

        int typeSigCount = accessor.ReadInt32();
        Dictionary<int, int> mapping = [];
        for (int i = 0; i < typeSigCount; i++)
        {
            (Guid, uint) sig = (accessor.ReadGuid(), accessor.ReadUInt32());
            if (!Mapping.TryAdd(sig, i + startTypes))
            {
                mapping.Add(i, Mapping[sig]);
            }
        }

        int typeDescriptorCount = accessor.ReadInt32();
        TypeDescriptors.Capacity = typeDescriptorCount + TypeDescriptors.Count;
        for (int i = 0; i < typeDescriptorCount; i++)
        {
            TypeDescriptors.Add(new EbxTypeDescriptor
            {
                NameHash = accessor.ReadUInt32(),
                FieldIndex = accessor.ReadInt32() + startFields,
                FieldCount = accessor.ReadUInt16(),
                Flags = accessor.ReadUInt16(),
                Size = accessor.ReadUInt16(),
                Alignment = accessor.ReadUInt16()
            });
        }

        int fieldDescriptorCount = accessor.ReadInt32();
        FieldDescriptors.Capacity = fieldDescriptorCount + FieldDescriptors.Count;
        for (int i = 0; i < fieldDescriptorCount; i++)
        {
            EbxFieldDescriptor field = new()
            {
                NameHash = accessor.ReadUInt32(),
                DataOffset = accessor.ReadUInt32(),
                Flags = accessor.ReadUInt16(),
                TypeDescriptorRef = accessor.ReadUInt16()
            };
            if (field.TypeDescriptorRef != ushort.MaxValue)
            {
                if (mapping.TryGetValue(field.TypeDescriptorRef, out int actual))
                {
                    field.TypeDescriptorRef = (ushort)actual;
                }
                else
                {
                    field.TypeDescriptorRef += (ushort)startTypes;
                }
            }

            FieldDescriptors.Add(field);
        }

        int unkCount = accessor.ReadInt32();
        for (int i = 0; i < unkCount; i++)
        {
            _ = accessor.ReadUInt32(); // Unk
            _ = accessor.ReadInt32(); // Count of unk2
            _ = accessor.ReadUInt32(); // Index, maps into unk2
        }

        int unk2Count = accessor.ReadInt32();
        for (int i = 0; i < unk2Count; i++)
        {
            _ = accessor.ReadUInt32(); // Offset, stringtable offset?
            _ = accessor.ReadInt32(); // Index, maps into signatures
        }

        if (fourCc == "RFL2")
        {
            _ = accessor.ReadInt32(); // Count of unk3
        }
    }
}