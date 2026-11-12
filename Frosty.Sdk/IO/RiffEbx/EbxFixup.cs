using Frosty.Sdk.IO.Ebx;

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

    public static EbxFixup ReadFixup(DataAccessor accessor)
    {
        EbxFixup fixup = new() { PartitionGuid = accessor.ReadGuid() };

        int typeGuidCount = accessor.ReadInt32();
        fixup.TypeGuids = new List<Guid>(typeGuidCount);
        for (int i = 0; i < typeGuidCount; i++)
        {
            fixup.TypeGuids.Add(accessor.ReadGuid());
        }

        fixup.TypeSignatures = ReadUInt32List(accessor);

        fixup.ExportedInstanceCount = accessor.ReadInt32();

        fixup.InstanceOffsets = ReadUInt32List(accessor);
        fixup.InstanceMapping = new Dictionary<uint, int>(fixup.InstanceOffsets.Count);
        for (int i = 0; i < fixup.InstanceOffsets.Count; i++)
        {
            fixup.InstanceMapping.Add(fixup.InstanceOffsets[i], i);
        }

        fixup.PointerOffsets = ReadUInt32List(accessor);
        fixup.ResourceRefOffsets = ReadUInt32List(accessor);

        int importCount = accessor.ReadInt32();
        fixup.Imports = new List<EbxImportReference>(importCount);
        fixup.Dependencies = new HashSet<Guid>(importCount);
        for (int i = 0; i < importCount; i++)
        {
            EbxImportReference import = new()
            {
                PartitionGuid = accessor.ReadGuid(), InstanceGuid = accessor.ReadGuid()
            };

            fixup.Imports.Add(import);
            fixup.Dependencies.Add(import.PartitionGuid);
        }

        fixup.ImportOffsets = ReadUInt32List(accessor);
        fixup.TypeInfoOffsets = ReadUInt32List(accessor);

        fixup.ArrayOffset = accessor.ReadUInt32();
        fixup.BoxedValueRefOffset = accessor.ReadUInt32();
        fixup.StringOffset = accessor.ReadUInt32();
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            accessor.ReadUInt32();
        }

        return fixup;
    }

    public static byte[] WriteFixup(EbxFixup fixup)
    {
        // Only the capacity: 16 bytes per GUID and 4 per count, offset or value
        using MemoryStream stream = new(((1 + fixup.TypeGuids.Count + (2 * fixup.Imports.Count)) * 16) + ((13 +
                fixup.TypeSignatures.Count + fixup.InstanceOffsets.Count + fixup.PointerOffsets.Count +
                fixup.ResourceRefOffsets.Count + fixup.ImportOffsets.Count + fixup.TypeInfoOffsets.Count) *
            sizeof(int)));
        using DataAccessor accessor = new(stream, true);
        accessor.WriteGuid(fixup.PartitionGuid);

        accessor.WriteInt32(fixup.TypeGuids.Count);
        foreach (Guid typeGuid in fixup.TypeGuids)
        {
            accessor.WriteGuid(typeGuid);
        }

        WriteUInt32List(accessor, fixup.TypeSignatures);

        accessor.WriteInt32(fixup.ExportedInstanceCount);

        WriteUInt32List(accessor, fixup.InstanceOffsets);
        WriteUInt32List(accessor, fixup.PointerOffsets);
        WriteUInt32List(accessor, fixup.ResourceRefOffsets);

        accessor.WriteInt32(fixup.Imports.Count);
        foreach (EbxImportReference import in fixup.Imports)
        {
            accessor.WriteGuid(import.PartitionGuid);
            accessor.WriteGuid(import.InstanceGuid);
        }

        WriteUInt32List(accessor, fixup.ImportOffsets);
        WriteUInt32List(accessor, fixup.TypeInfoOffsets);

        accessor.WriteUInt32(fixup.ArrayOffset);
        accessor.WriteUInt32(fixup.BoxedValueRefOffset);
        accessor.WriteUInt32(fixup.StringOffset);
        if (ProfilesLibrary.FrostbiteVersion >= "2021")
        {
            accessor.WriteUInt32(0);
        }

        return stream.ToArray();
    }

    // An int32 count followed by that many uint32 values
    private static List<uint> ReadUInt32List(DataAccessor accessor)
    {
        int count = accessor.ReadInt32();
        List<uint> values = new(count);
        for (int i = 0; i < count; i++)
        {
            values.Add(accessor.ReadUInt32());
        }

        return values;
    }

    private static void WriteUInt32List(DataAccessor accessor, List<uint> values)
    {
        accessor.WriteInt32(values.Count);
        foreach (uint value in values)
        {
            accessor.WriteUInt32(value);
        }
    }
}