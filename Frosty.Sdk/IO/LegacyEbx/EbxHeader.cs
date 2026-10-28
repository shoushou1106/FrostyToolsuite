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

    public static EbxHeader ReadHeader(DataAccessor inAccessor)
    {
        EbxHeader header = new()
        {
            Magic = (EbxVersion)inAccessor.ReadUInt32(),
            StringsOffset = inAccessor.ReadUInt32(),
            StringsAndDataLength = inAccessor.ReadUInt32(),
            ImportCount = inAccessor.ReadInt32(),
            InstanceCount = inAccessor.ReadUInt16(),
            ExportedInstanceCount = inAccessor.ReadUInt16(),
            UniqueTypeCount = inAccessor.ReadUInt16(),
            TypeDescriptorCount = inAccessor.ReadUInt16(),
            FieldDescriptorCount = inAccessor.ReadUInt16(),
            TypeNameTableLength = inAccessor.ReadUInt16(),
            StringTableLength = inAccessor.ReadUInt32(),
            ArrayCount = inAccessor.ReadInt32(),
            DataLength = inAccessor.ReadUInt32(),
            PartitionGuid = inAccessor.ReadGuid()
        };

        header.ArrayOffset = header.StringsOffset + header.StringTableLength + header.DataLength;

        if (header.Magic == EbxVersion.Version4)
        {
            header.BoxedValueCount = inAccessor.ReadInt32();
            header.BoxedValueOffset = inAccessor.ReadUInt32();
            header.BoxedValueOffset += header.StringsOffset + header.StringTableLength;
        }
        else
        {
            inAccessor.Pad(16);
        }

        header.Imports = new EbxImportReference[header.ImportCount];
        header.Dependencies = new HashSet<Guid>(header.ImportCount);
        for (int i = 0; i < header.ImportCount; i++)
        {
            EbxImportReference import = new()
            {
                PartitionGuid = inAccessor.ReadGuid(),
                InstanceGuid = inAccessor.ReadGuid()
            };

            header.Imports[i] = import;
            header.Dependencies.Add(import.PartitionGuid);
        }

        Dictionary<int, string> typeNames = new();

        long typeNamesOffset = inAccessor.Position;
        while (inAccessor.Position - typeNamesOffset < header.TypeNameTableLength)
        {
            string typeName = inAccessor.ReadNullTerminatedString();
            int hash = Fnv1.HashString(typeName);

            typeNames.TryAdd(hash, typeName);
        }

        header.FieldDescriptors = new EbxFieldDescriptor[header.FieldDescriptorCount];
        for (int i = 0; i < header.FieldDescriptorCount; i++)
        {
            EbxFieldDescriptor fieldDescriptor = new()
            {
                NameHash = inAccessor.ReadUInt32(),
                Flags = inAccessor.ReadUInt16(),
                TypeDescriptorRef = inAccessor.ReadUInt16(),
                DataOffset = inAccessor.ReadUInt32(),
                SecondOffset = inAccessor.ReadUInt32()
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
                NameHash = inAccessor.ReadUInt32(),
                FieldIndex = inAccessor.ReadInt32(),
                FieldCount = inAccessor.ReadByte(),
                Alignment = inAccessor.ReadByte(),
                Flags = inAccessor.ReadUInt16(),
                Size = inAccessor.ReadUInt16(),
                SecondSize = inAccessor.ReadUInt16(),
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
                TypeDescriptorRef = inAccessor.ReadUInt16(),
                Count = inAccessor.ReadUInt16(),
                IsExported = i < header.ExportedInstanceCount
            };
        }

        inAccessor.Pad(16);

        header.Arrays = new EbxArray[header.ArrayCount];
        for (int i = 0; i < header.ArrayCount; i++)
        {
            header.Arrays[i] = new EbxArray
            {
                Offset = inAccessor.ReadUInt32(),
                Count = inAccessor.ReadInt32(),
                TypeDescriptorRef = inAccessor.ReadInt32()
            };
        }

        inAccessor.Pad(16);

        header.BoxedValues = new EbxBoxedValue[header.BoxedValueCount];
        for (int i = 0; i < header.BoxedValueCount; i++)
        {
            header.BoxedValues[i] = new EbxBoxedValue
            {
                Offset = inAccessor.ReadUInt32(),
                TypeDescriptorRef = inAccessor.ReadUInt16(),
                Type = inAccessor.ReadUInt16()
            };
        }

        return header;
    }
}