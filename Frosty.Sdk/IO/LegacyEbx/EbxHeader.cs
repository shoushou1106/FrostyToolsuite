using Frosty.Sdk.IO.Ebx;
using Frosty.Sdk.Utils;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global

namespace Frosty.Sdk.IO.LegacyEbx;

internal struct EbxHeader
{
    public EbxVersion Magic;
    public uint StringsOffset;
    public uint StringsAndDataLength;
    public int ImportCount;
    public ushort InstanceCount;
    public ushort ExportedInstanceCount;
    public ushort UniqueTypeCount;
    public ushort TypeDescriptorCount;
    public ushort FieldDescriptorCount;
    public ushort TypeNameTableLength;

    public uint StringTableLength;

    public int ArrayCount;
    public uint ArrayOffset;

    public uint DataLength;

    public Guid PartitionGuid;

    // Only in Version4
    public int BoxedValueCount;
    public uint BoxedValueOffset;

    public EbxImportReference[] Imports;
    public HashSet<Guid> Dependencies;

    public EbxFieldDescriptor[] FieldDescriptors;
    public EbxTypeDescriptor[] TypeDescriptors;

    public EbxInstance[] Instances;

    public EbxArray[] Arrays;

    // Only in Version4
    public EbxBoxedValue[] BoxedValues;

    public static EbxHeader ReadHeader(DataAccessor accessor)
    {
        EbxHeader header = new()
        {
            Magic = (EbxVersion)accessor.ReadUInt32(),
            StringsOffset = accessor.ReadUInt32(),
            StringsAndDataLength = accessor.ReadUInt32(),
            ImportCount = accessor.ReadInt32(),
            InstanceCount = accessor.ReadUInt16(),
            ExportedInstanceCount = accessor.ReadUInt16(),
            UniqueTypeCount = accessor.ReadUInt16(),
            TypeDescriptorCount = accessor.ReadUInt16(),
            FieldDescriptorCount = accessor.ReadUInt16(),
            TypeNameTableLength = accessor.ReadUInt16(),
            StringTableLength = accessor.ReadUInt32(),
            ArrayCount = accessor.ReadInt32(),
            DataLength = accessor.ReadUInt32(),
            PartitionGuid = accessor.ReadGuid()
        };

        header.ArrayOffset = header.StringsOffset + header.StringTableLength + header.DataLength;

        if (header.Magic == EbxVersion.Version4)
        {
            header.BoxedValueCount = accessor.ReadInt32();
            header.BoxedValueOffset = accessor.ReadUInt32();
            header.BoxedValueOffset += header.StringsOffset + header.StringTableLength;
        }
        else
        {
            accessor.Pad(16);
        }

        header.Imports = new EbxImportReference[header.ImportCount];
        header.Dependencies = new HashSet<Guid>(header.ImportCount);
        for (int i = 0; i < header.ImportCount; i++)
        {
            EbxImportReference import = new()
            {
                PartitionGuid = accessor.ReadGuid(), InstanceGuid = accessor.ReadGuid()
            };

            header.Imports[i] = import;
            header.Dependencies.Add(import.PartitionGuid);
        }

        Dictionary<int, string> typeNames = [];

        long typeNamesOffset = accessor.Position;
        while (accessor.Position - typeNamesOffset < header.TypeNameTableLength)
        {
            string typeName = accessor.ReadNullTerminatedString();
            int hash = Fnv1.HashString(typeName);

            typeNames.TryAdd(hash, typeName);
        }

        header.FieldDescriptors = new EbxFieldDescriptor[header.FieldDescriptorCount];
        for (int i = 0; i < header.FieldDescriptorCount; i++)
        {
            EbxFieldDescriptor fieldDescriptor = new()
            {
                NameHash = accessor.ReadUInt32(),
                Flags = accessor.ReadUInt16(),
                TypeDescriptorRef = accessor.ReadUInt16(),
                DataOffset = accessor.ReadUInt32(),
                SecondOffset = accessor.ReadUInt32()
            };

            fieldDescriptor.Name = typeNames.TryGetValue((int)fieldDescriptor.NameHash, out string? value)
                ? value
                : string.Empty;

            header.FieldDescriptors[i] = fieldDescriptor;
        }

        header.TypeDescriptors = new EbxTypeDescriptor[header.TypeDescriptorCount];
        for (int i = 0; i < header.TypeDescriptorCount; i++)
        {
            EbxTypeDescriptor typeDescriptor = new()
            {
                NameHash = accessor.ReadUInt32(),
                FieldIndex = accessor.ReadInt32(),
                FieldCount = accessor.ReadByte(),
                Alignment = accessor.ReadByte(),
                Flags = accessor.ReadUInt16(),
                Size = accessor.ReadUInt16(),
                SecondSize = accessor.ReadUInt16(),
                Index = -1
            };

            typeDescriptor.Name = typeNames.TryGetValue((int)typeDescriptor.NameHash, out string? value)
                ? value
                : string.Empty;

            header.TypeDescriptors[i] = typeDescriptor;
        }

        header.Instances = new EbxInstance[header.InstanceCount];
        for (int i = 0; i < header.InstanceCount; i++)
        {
            header.Instances[i] = new EbxInstance
            {
                TypeDescriptorRef = accessor.ReadUInt16(),
                Count = accessor.ReadUInt16(),
                IsExported = i < header.ExportedInstanceCount
            };
        }

        accessor.Pad(16);

        header.Arrays = new EbxArray[header.ArrayCount];
        for (int i = 0; i < header.ArrayCount; i++)
        {
            header.Arrays[i] = new EbxArray
            {
                Offset = accessor.ReadUInt32(),
                Count = accessor.ReadInt32(),
                TypeDescriptorRef = accessor.ReadInt32()
            };
        }

        accessor.Pad(16);

        header.BoxedValues = new EbxBoxedValue[header.BoxedValueCount];
        for (int i = 0; i < header.BoxedValueCount; i++)
        {
            header.BoxedValues[i] = new EbxBoxedValue
            {
                Offset = accessor.ReadUInt32(),
                TypeDescriptorRef = accessor.ReadUInt16(),
                Type = accessor.ReadUInt16()
            };
        }

        return header;
    }
}