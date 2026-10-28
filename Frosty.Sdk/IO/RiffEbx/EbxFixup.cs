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

    public static EbxFixup ReadFixup(DataAccessor inAccessor)
    {
        EbxFixup fixup = new() { PartitionGuid = inAccessor.ReadGuid() };

        int typeGuidCount = inAccessor.ReadInt32();
        fixup.TypeGuids = new List<Guid>(typeGuidCount);
        for (int i = 0; i < typeGuidCount; i++)
        {
            fixup.TypeGuids.Add(inAccessor.ReadGuid());
        }

        fixup.TypeSignatures = ReadUInt32List(inAccessor);

        fixup.ExportedInstanceCount = inAccessor.ReadInt32();

        fixup.InstanceOffsets = ReadUInt32List(inAccessor);
        fixup.InstanceMapping = new Dictionary<uint, int>(fixup.InstanceOffsets.Count);
        for (int i = 0; i < fixup.InstanceOffsets.Count; i++)
        {
            fixup.InstanceMapping.Add(fixup.InstanceOffsets[i], i);
        }

        fixup.PointerOffsets = ReadUInt32List(inAccessor);
        fixup.ResourceRefOffsets = ReadUInt32List(inAccessor);

        int importCount = inAccessor.ReadInt32();
        fixup.Imports = new List<EbxImportReference>(importCount);
        fixup.Dependencies = new HashSet<Guid>(importCount);
        for (int i = 0; i < importCount; i++)
        {
            EbxImportReference import = new()
            {
                PartitionGuid = inAccessor.ReadGuid(),
                InstanceGuid = inAccessor.ReadGuid()
            };

            fixup.Imports.Add(import);
            fixup.Dependencies.Add(import.PartitionGuid);
        }

        fixup.ImportOffsets = ReadUInt32List(inAccessor);
        fixup.TypeInfoOffsets = ReadUInt32List(inAccessor);

        fixup.ArrayOffset = inAccessor.ReadUInt32();
        fixup.BoxedValueRefOffset = inAccessor.ReadUInt32();
        fixup.StringOffset = inAccessor.ReadUInt32();
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            inAccessor.ReadUInt32();
        }

        return fixup;
    }

    public static Block<byte> WriteFixup(EbxFixup inFixup)
    {
        Block<byte> retVal = new((1 + inFixup.TypeGuids.Count + 2 * inFixup.Imports.Count) * 16 + (12 +
                inFixup.TypeSignatures.Count + inFixup.InstanceOffsets.Count + inFixup.PointerOffsets.Count +
                inFixup.ResourceRefOffsets.Count + inFixup.ImportOffsets.Count + inFixup.TypeInfoOffsets.Count) *
            sizeof(int));
        using BlockAccessor accessor = new(retVal, true);
        accessor.WriteGuid(inFixup.PartitionGuid);

        accessor.WriteInt32(inFixup.TypeGuids.Count);
        foreach (Guid typeGuid in inFixup.TypeGuids)
        {
            accessor.WriteGuid(typeGuid);
        }

        WriteUInt32List(accessor, inFixup.TypeSignatures);

        accessor.WriteInt32(inFixup.ExportedInstanceCount);

        WriteUInt32List(accessor, inFixup.InstanceOffsets);
        WriteUInt32List(accessor, inFixup.PointerOffsets);
        WriteUInt32List(accessor, inFixup.ResourceRefOffsets);

        accessor.WriteInt32(inFixup.Imports.Count);
        foreach (EbxImportReference import in inFixup.Imports)
        {
            accessor.WriteGuid(import.PartitionGuid);
            accessor.WriteGuid(import.InstanceGuid);
        }

        WriteUInt32List(accessor, inFixup.ImportOffsets);
        WriteUInt32List(accessor, inFixup.TypeInfoOffsets);

        accessor.WriteUInt32(inFixup.ArrayOffset);
        accessor.WriteUInt32(inFixup.BoxedValueRefOffset);
        accessor.WriteUInt32(inFixup.StringOffset);
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            accessor.WriteUInt32(0);
        }

        return retVal;
    }

    // An int32 count followed by that many uint32 values
    private static List<uint> ReadUInt32List(DataAccessor inAccessor)
    {
        int count = inAccessor.ReadInt32();
        List<uint> values = new(count);
        for (int i = 0; i < count; i++)
        {
            values.Add(inAccessor.ReadUInt32());
        }

        return values;
    }

    private static void WriteUInt32List(DataAccessor inAccessor, List<uint> inValues)
    {
        inAccessor.WriteInt32(inValues.Count);
        foreach (uint value in inValues)
        {
            inAccessor.WriteUInt32(value);
        }
    }
}
