using Frosty.Sdk.IO.Ebx;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.RiffEbx;

internal struct EbxFixup
{
    public Guid PartitionGuid;
    public List<Guid> TypeGuids;
    public List<uint> TypeSignatures;
    public int ExportedInstanceCount;
    public List<uint> InstanceOffsets;
    public Dictionary<uint, int> InstanceMapping;
    public List<uint> PointerOffsets;
    public List<uint> ResourceRefOffsets;
    public List<EbxImportReference> Imports;
    public HashSet<Guid> Dependencies;
    public List<uint> ImportOffsets;
    public List<uint> TypeInfoOffsets;
    public uint ArrayOffset;
    public uint BoxedValueRefOffset;
    public uint StringOffset;

    public static EbxFixup ReadFixup(DataStream inStream)
    {
        EbxFixup fixup = new() { PartitionGuid = inStream.ReadGuid() };

        int typeGuidCount = inStream.ReadInt32();
        fixup.TypeGuids = new List<Guid>(typeGuidCount);
        for (int i = 0; i < typeGuidCount; i++)
        {
            fixup.TypeGuids.Add(inStream.ReadGuid());
        }

        fixup.TypeSignatures = ReadUInt32List(inStream);

        fixup.ExportedInstanceCount = inStream.ReadInt32();

        fixup.InstanceOffsets = ReadUInt32List(inStream);
        fixup.InstanceMapping = new Dictionary<uint, int>(fixup.InstanceOffsets.Count);
        for (int i = 0; i < fixup.InstanceOffsets.Count; i++)
        {
            fixup.InstanceMapping.Add(fixup.InstanceOffsets[i], i);
        }

        fixup.PointerOffsets = ReadUInt32List(inStream);
        fixup.ResourceRefOffsets = ReadUInt32List(inStream);

        int importCount = inStream.ReadInt32();
        fixup.Imports = new List<EbxImportReference>(importCount);
        fixup.Dependencies = new HashSet<Guid>(importCount);
        for (int i = 0; i < importCount; i++)
        {
            EbxImportReference import = new()
            {
                PartitionGuid = inStream.ReadGuid(),
                InstanceGuid = inStream.ReadGuid()
            };

            fixup.Imports.Add(import);
            fixup.Dependencies.Add(import.PartitionGuid);
        }

        fixup.ImportOffsets = ReadUInt32List(inStream);
        fixup.TypeInfoOffsets = ReadUInt32List(inStream);

        fixup.ArrayOffset = inStream.ReadUInt32();
        fixup.BoxedValueRefOffset = inStream.ReadUInt32();
        fixup.StringOffset = inStream.ReadUInt32();
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            inStream.ReadUInt32();
        }

        return fixup;
    }

    public static Block<byte> WriteFixup(EbxFixup inFixup)
    {
        Block<byte> retVal = new((1 + inFixup.TypeGuids.Count + 2 * inFixup.Imports.Count) * 16 + (12 +
                inFixup.TypeSignatures.Count + inFixup.InstanceOffsets.Count + inFixup.PointerOffsets.Count +
                inFixup.ResourceRefOffsets.Count + inFixup.ImportOffsets.Count + inFixup.TypeInfoOffsets.Count) *
            sizeof(int));
        using BlockStream stream = new(retVal, true);
        stream.WriteGuid(inFixup.PartitionGuid);

        stream.WriteInt32(inFixup.TypeGuids.Count);
        foreach (Guid typeGuid in inFixup.TypeGuids)
        {
            stream.WriteGuid(typeGuid);
        }

        WriteUInt32List(stream, inFixup.TypeSignatures);

        stream.WriteInt32(inFixup.ExportedInstanceCount);

        WriteUInt32List(stream, inFixup.InstanceOffsets);
        WriteUInt32List(stream, inFixup.PointerOffsets);
        WriteUInt32List(stream, inFixup.ResourceRefOffsets);

        stream.WriteInt32(inFixup.Imports.Count);
        foreach (EbxImportReference import in inFixup.Imports)
        {
            stream.WriteGuid(import.PartitionGuid);
            stream.WriteGuid(import.InstanceGuid);
        }

        WriteUInt32List(stream, inFixup.ImportOffsets);
        WriteUInt32List(stream, inFixup.TypeInfoOffsets);

        stream.WriteUInt32(inFixup.ArrayOffset);
        stream.WriteUInt32(inFixup.BoxedValueRefOffset);
        stream.WriteUInt32(inFixup.StringOffset);
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            stream.WriteUInt32(0);
        }

        return retVal;
    }

    // An int32 count followed by that many uint32 values
    private static List<uint> ReadUInt32List(DataStream inStream)
    {
        int count = inStream.ReadInt32();
        List<uint> values = new(count);
        for (int i = 0; i < count; i++)
        {
            values.Add(inStream.ReadUInt32());
        }

        return values;
    }

    private static void WriteUInt32List(DataStream inStream, List<uint> inValues)
    {
        inStream.WriteInt32(inValues.Count);
        foreach (uint value in inValues)
        {
            inStream.WriteUInt32(value);
        }
    }
}
