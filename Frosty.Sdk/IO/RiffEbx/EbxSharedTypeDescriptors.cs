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

    public static EbxTypeDescriptor GetTypeDescriptor(Guid inGuid, uint inSignature) => TypeDescriptors[Mapping[(inGuid, inSignature)]];

    public static EbxTypeDescriptor GetTypeDescriptor(ushort inIndex) => TypeDescriptors[inIndex];

    public static EbxFieldDescriptor GetFieldDescriptors(int inIndex) => FieldDescriptors[inIndex];

    private static void Read(Block<byte> inFile)
    {
        using BlockStream stream = new(inFile, true);
        RiffStream riffStream = new(stream);
        riffStream.ReadHeader(out FourCc fourCc, out uint size);

        if (fourCc != "EBXT")
        {
            throw new InvalidDataException("Not a valid EBXT chunk");
        }

        // Read REFL chunk
        riffStream.ReadNextChunk(ref size, ProcessChunk);

        Debug.Assert(riffStream.Eof);
    }

    private static void ProcessChunk(DataStream inStream, FourCc inFourCc, uint inSize)
    {
        if (inFourCc != "REFL" && inFourCc != "RFL2")
        {
            return;
        }

        int startFields = FieldDescriptors.Count;
        int startTypes = TypeDescriptors.Count;

        int typeSigCount = inStream.ReadInt32();
        Dictionary<int, int> mapping = new();
        for (int i = 0; i < typeSigCount; i++)
        {
            (Guid, uint) sig = (inStream.ReadGuid(), inStream.ReadUInt32());
            if (!Mapping.TryAdd(sig, i + startTypes))
            {
                mapping.Add(i, Mapping[sig]);
            }
        }

        int typeDescriptorCount = inStream.ReadInt32();
        TypeDescriptors.Capacity = typeDescriptorCount + TypeDescriptors.Count;
        for (int i = 0; i < typeDescriptorCount; i++)
        {
            TypeDescriptors.Add(new EbxTypeDescriptor
            {
                NameHash = inStream.ReadUInt32(),
                FieldIndex = inStream.ReadInt32() + startFields,
                FieldCount = inStream.ReadUInt16(),
                Flags = inStream.ReadUInt16(),
                Size = inStream.ReadUInt16(),
                Alignment = inStream.ReadUInt16()
            });
        }

        int fieldDescriptorCount = inStream.ReadInt32();
        FieldDescriptors.Capacity = fieldDescriptorCount + FieldDescriptors.Count;
        for (int i = 0; i < fieldDescriptorCount; i++)
        {
            EbxFieldDescriptor field = new()
            {
                NameHash = inStream.ReadUInt32(),
                DataOffset = inStream.ReadUInt32(),
                Flags = inStream.ReadUInt16(),
                TypeDescriptorRef = inStream.ReadUInt16()
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

        int unkCount = inStream.ReadInt32();
        for (int i = 0; i < unkCount; i++)
        {
            _ = inStream.ReadUInt32(); // Unk
            _ = inStream.ReadInt32(); // Count of unk2
            _ = inStream.ReadUInt32(); // Index, maps into unk2
        }

        int unk2Count = inStream.ReadInt32();
        for (int i = 0; i < unk2Count; i++)
        {
            _ = inStream.ReadUInt32(); // Offset, stringtable offset?
            _ = inStream.ReadInt32(); // Index, maps into signatures
        }

        if (inFourCc == "RFL2")
        {
            _ = inStream.ReadInt32(); // Count of unk3
        }
    }
}