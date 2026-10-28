using System.Diagnostics;

using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.RiffEbx;

internal static class EbxSharedTypeDescriptors
{
    private static bool s_isInitialized;

    private static readonly Dictionary<(Guid, uint), int> Mapping = new();
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

    public static EbxTypeDescriptor GetTypeDescriptor(Guid inGuid, uint inSignature)
    {
        return TypeDescriptors[Mapping[(inGuid, inSignature)]];
    }

    public static EbxTypeDescriptor GetTypeDescriptor(ushort inIndex)
    {
        return TypeDescriptors[inIndex];
    }

    public static EbxFieldDescriptor GetFieldDescriptors(int inIndex)
    {
        return FieldDescriptors[inIndex];
    }

    private static void Read(Block<byte> inFile)
    {
        using BlockAccessor accessor = new(inFile, true);
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

    private static void ProcessChunk(DataAccessor inAccessor, FourCc inFourCc, uint inSize)
    {
        if (inFourCc != "REFL" && inFourCc != "RFL2")
        {
            return;
        }

        int startFields = FieldDescriptors.Count;
        int startTypes = TypeDescriptors.Count;

        int typeSigCount = inAccessor.ReadInt32();
        Dictionary<int, int> mapping = new();
        for (int i = 0; i < typeSigCount; i++)
        {
            (Guid, uint) sig = (inAccessor.ReadGuid(), inAccessor.ReadUInt32());
            if (!Mapping.TryAdd(sig, i + startTypes))
            {
                mapping.Add(i, Mapping[sig]);
            }
        }

        int typeDescriptorCount = inAccessor.ReadInt32();
        TypeDescriptors.Capacity = typeDescriptorCount + TypeDescriptors.Count;
        for (int i = 0; i < typeDescriptorCount; i++)
        {
            TypeDescriptors.Add(new EbxTypeDescriptor
            {
                NameHash = inAccessor.ReadUInt32(),
                FieldIndex = inAccessor.ReadInt32() + startFields,
                FieldCount = inAccessor.ReadUInt16(),
                Flags = inAccessor.ReadUInt16(),
                Size = inAccessor.ReadUInt16(),
                Alignment = inAccessor.ReadUInt16()
            });
        }

        int fieldDescriptorCount = inAccessor.ReadInt32();
        FieldDescriptors.Capacity = fieldDescriptorCount + FieldDescriptors.Count;
        for (int i = 0; i < fieldDescriptorCount; i++)
        {
            EbxFieldDescriptor field = new()
            {
                NameHash = inAccessor.ReadUInt32(),
                DataOffset = inAccessor.ReadUInt32(),
                Flags = inAccessor.ReadUInt16(),
                TypeDescriptorRef = inAccessor.ReadUInt16()
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

        int unkCount = inAccessor.ReadInt32();
        for (int i = 0; i < unkCount; i++)
        {
            _ = inAccessor.ReadUInt32(); // Unk
            _ = inAccessor.ReadInt32(); // Count of unk2
            _ = inAccessor.ReadUInt32(); // Index, maps into unk2
        }

        int unk2Count = inAccessor.ReadInt32();
        for (int i = 0; i < unk2Count; i++)
        {
            _ = inAccessor.ReadUInt32(); // Offset, stringtable offset?
            _ = inAccessor.ReadInt32(); // Index, maps into signatures
        }

        if (inFourCc == "RFL2")
        {
            _ = inAccessor.ReadInt32(); // Count of unk3
        }
    }
}