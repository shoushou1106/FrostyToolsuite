using System.Collections;
using System.Reflection;

using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Ebx;
using Frosty.Sdk.TypeSdk;
using Frosty.Sdk.Utils;

using static Frosty.Sdk.TypeSdk.TypeFlags;

using TypeInfo = Frosty.Sdk.TypeSdk.TypeInfo;

namespace Frosty.Sdk.IO.LegacyEbx;

// _data only lives between building the payload and writing it out, which disposes it
// CA1001: Types that own disposable fields should be disposable
#pragma warning disable CA1001
public sealed class EbxWriter : BaseEbxWriter
#pragma warning restore CA1001
{
    private const long HeaderStringsOffsetPos = 0x4;
    private const long HeaderTypeNamesLenPos = 0x1A;
    private const long HeaderDataLenPos = 0x24;
    private const long HeaderBoxedValuesPos = 0x3C;

    private readonly List<EbxArray> _arrays = [];
    private readonly List<EbxBoxedValue> _boxedValues = [];
    private readonly List<EbxFieldDescriptor> _fieldDescriptors = [];
    private readonly List<EbxInstance> _instances = [];

    private readonly List<EbxTypeDescriptor> _typeDescriptors = [];
    private readonly HashSet<string> _typeNames = [];

    private readonly EbxTypeResolver _typeResolver;

    private Block<byte>? _data;
    private ushort _numExports;

    private ushort _uniqueClassCount;

    public EbxWriter(DataAccessor inAccessor)
        : base(inAccessor)
    {
        _typeResolver = new EbxTypeResolver(_typeDescriptors, _fieldDescriptors);
    }

    protected override void InternalWriteEbx(Guid inPartitionGuid, int inExportedInstanceCount)
    {
        ProcessData();

        WriteHeader(inPartitionGuid);

        if (ProfilesLibrary.EbxVersion >= 4)
        {
            _accessor.WriteInt32(_boxedValues.Count);
            _accessor.WriteUInt32(0xDEADBEEF);
        }
        else
        {
            _accessor.Pad(16);
        }

        foreach (EbxImportReference importRef in _imports)
        {
            _accessor.WriteGuid(importRef.PartitionGuid);
            _accessor.WriteGuid(importRef.InstanceGuid);
        }

        _accessor.Pad(16);

        long offset = _accessor.Position;
        foreach (string name in _typeNames)
        {
            _accessor.WriteNullTerminatedString(name);
        }

        _accessor.Pad(16);

        ushort typeNamesLen = (ushort)(_accessor.Position - offset);

        foreach (EbxFieldDescriptor fieldType in _fieldDescriptors)
        {
            _accessor.WriteUInt32(fieldType.NameHash);
            _accessor.WriteUInt16(fieldType.Flags);
            _accessor.WriteUInt16(fieldType.TypeDescriptorRef);
            _accessor.WriteUInt32(fieldType.DataOffset);
            _accessor.WriteUInt32(fieldType.SecondOffset);
        }

        foreach (EbxTypeDescriptor classType in _typeDescriptors)
        {
            _accessor.WriteUInt32(classType.NameHash);
            _accessor.WriteInt32(classType.FieldIndex);
            _accessor.WriteByte(classType.FieldCount);
            _accessor.WriteByte(classType.Alignment);
            _accessor.WriteUInt16(classType.Flags);
            _accessor.WriteUInt16(classType.Size);
            _accessor.WriteUInt16(classType.SecondSize);
        }

        foreach (EbxInstance instance in _instances)
        {
            _accessor.WriteUInt16(instance.TypeDescriptorRef);
            _accessor.WriteUInt16(instance.Count);
        }

        _accessor.Pad(16);

        long arrayPos = _accessor.Position;
        for (int i = 0; i < _arrays.Count; i++)
        {
            _accessor.WriteUInt32(_arrays[i].Offset);
            _accessor.WriteInt32(_arrays[i].Count);
            _accessor.WriteInt32(_arrays[i].TypeDescriptorRef);
        }

        _accessor.Pad(16);

        long boxedValuesPos = _accessor.Position;
        for (int i = 0; i < _boxedValues.Count; i++)
        {
            _accessor.WriteUInt32(_boxedValues[i].Offset);
            _accessor.WriteUInt16(_boxedValues[i].TypeDescriptorRef);
            _accessor.WriteUInt16(_boxedValues[i].Type);
        }

        _accessor.Pad(16);

        uint stringsOffset = (uint)_accessor.Position;
        foreach (string str in _strings)
        {
            _accessor.WriteNullTerminatedString(str);
        }

        _accessor.Pad(16);

        _stringsLength = (uint)(_accessor.Position - stringsOffset);

        offset = _accessor.Position;
        _accessor.Write(_data!);
        _data!.Dispose();
        _accessor.WriteByte(0);

        _accessor.Pad(16);

        uint dataLen = (uint)(_accessor.Position - offset);

        if (_arrayData.Count > 0)
        {
            offset = _accessor.Position;
            for (int i = 0, j = 0; i < _arrays.Count; i++)
            {
                EbxArray array = _arrays[i];
                if (array.Count == 0)
                {
                    continue;
                }

                // Make sure the array data is padded correctly for the first item
                // TODO: Proper alignment
                _accessor.Position += sizeof(int);
                _accessor.Pad(16);
                _accessor.Position -= sizeof(int);
                _accessor.WriteInt32(array.Count);

                array.Offset = (uint)(_accessor.Position - offset);
                _accessor.StepIn(arrayPos + (i * 12));
                _accessor.WriteUInt32(array.Offset);
                _accessor.StepOut();

                _accessor.Write(_arrayData[j]);
                _arrayData[j++].Dispose();
            }

            _accessor.Pad(16);
        }

        uint boxedValueOffset = (uint)(_accessor.Position - _stringsLength - stringsOffset);
        if (_boxedValues.Count > 0)
        {
            for (int i = 0; i < _boxedValues.Count; i++)
            {
                EbxBoxedValue boxedValue = _boxedValues[i];

                // TODO: Proper alignment
                _accessor.Pad(16);

                boxedValue.Offset = (uint)(_accessor.Position - boxedValueOffset - _stringsLength - stringsOffset);
                _accessor.StepIn(boxedValuesPos + (i * 8));
                _accessor.WriteUInt32(boxedValue.Offset);
                _accessor.StepOut();

                _accessor.Write(_boxedValueData[i]);
                _boxedValueData[i].Dispose();

                _boxedValues[i] = boxedValue;
            }
        }

        _accessor.Pad(16);

        uint stringsAndDataLen = (uint)(_accessor.Position - stringsOffset);

        _accessor.Position = HeaderStringsOffsetPos;
        _accessor.WriteUInt32(stringsOffset);
        _accessor.WriteUInt32(stringsAndDataLen);

        _accessor.Position = HeaderTypeNamesLenPos;
        _accessor.WriteUInt16(typeNamesLen);
        _accessor.WriteUInt32(_stringsLength);

        _accessor.Position = HeaderDataLenPos;
        _accessor.WriteUInt32(dataLen);

        if (ProfilesLibrary.EbxVersion >= 4)
        {
            _accessor.Position = HeaderBoxedValuesPos;
            _accessor.WriteUInt32(boxedValueOffset);
        }

        _accessor.Position = stringsOffset + stringsAndDataLen;
    }

    protected override int CompareInstances(IEbxInstance inA, IEbxInstance inB)
    {
        ArgumentNullException.ThrowIfNull(inA);
        ArgumentNullException.ThrowIfNull(inB);

        return string.Compare(inA.GetType().GetName(), inB.GetType().GetName(), StringComparison.Ordinal);
    }

    protected override int AddType(Type inType)
    {
        ArgumentNullException.ThrowIfNull(inType);

        if (_useSharedTypeDescriptors)
        {
            _typeDescriptors.Add(EbxSharedTypeDescriptors.GetKey(inType.GetNameHash()));
            _typeToDescriptor.Add(inType.GetNameHash(), _typeDescriptors.Count - 1);

            return _typeDescriptors.Count - 1;
        }

        int index;
        EbxTypeMetaAttribute? typeMeta = inType.GetCustomAttribute<EbxTypeMetaAttribute>();

        if (inType.IsEnum)
        {
            string[] enumNames = inType.GetEnumNames();
            Array enumValues = inType.GetEnumValues();

            string name = inType.GetName();

            uint nameHash = (uint)Fnv1.HashString(name);

            index = AddType(name,
                nameHash,
                _fieldDescriptors.Count,
                (byte)enumNames.Length,
                4,
                typeMeta!.Flags,
                4,
                0);

            for (int i = 0; i < enumNames.Length; i++)
            {
                ReserveFields(1);
                int enumValue = (int)enumValues.GetValue(i)!;
                AddField(enumNames[i], (uint)Fnv1.HashString(enumNames[i]), new TypeFlags(TypeEnum.Int32, unk: 0), 0,
                    (uint)enumValue,
                    (uint)enumValue, _fieldDescriptors.Count - 1);
            }
        }
        else if (inType.Name.Equals(CollectionName, StringComparison.Ordinal))
        {
            Type elementType = inType.GenericTypeArguments[0].Name == "PointerRef"
                ? DataContainerType
                : inType.GenericTypeArguments[0];

            string name = inType.GetName();

            uint nameHash = (uint)Fnv1.HashString(name);

            index = AddType(name, nameHash, _fieldDescriptors.Count, 1, 4,
                elementType.GetCustomAttribute<EbxArrayMetaAttribute>()!.Flags, 4, 0);

            ReserveFields(1);

            ushort arrayClassRef =
                (ushort)(typeof(IPrimitive).IsAssignableFrom(elementType) ? 0 : FindOrAddType(elementType));

            AddField("member", (uint)Fnv1.HashString("member"),
                elementType.GetCustomAttribute<EbxTypeMetaAttribute>()!.Flags,
                arrayClassRef, 0, 0, _typeDescriptors[index].FieldIndex);
        }
        else if (inType.IsClass)
        {
            bool inherited = false;
            ushort superClassRef = 0;
            if (inType.BaseType!.Namespace!.StartsWith(EbxNamespace, StringComparison.Ordinal))
            {
                superClassRef = FindOrAddType(inType.BaseType);
                inherited = true;
            }

            PropertyInfo[] allProps =
                inType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            // Ignore transients if saving to project
            List<PropertyInfo> classProperties =
                [.. allProps.Where(static pi => pi.GetCustomAttribute<IsTransientAttribute>() is null)];
            string name = inType.GetName();

            uint nameHash = (uint)Fnv1.HashString(name);
            index = AddType(name, nameHash, _fieldDescriptors.Count, (byte)classProperties.Count, 4, typeMeta!.Flags,
                8, 0);

            EbxTypeDescriptor typeDesc = _typeDescriptors[index];

            ReserveFields(typeDesc.GetFieldCount());

            int fieldIndex = 0;
            bool hasFirstField = true;

            if (inherited)
            {
                ReserveFields(1);

                uint firstOffset = 8;
                if (_typeDescriptors[superClassRef].Size > 8)
                {
                    hasFirstField = false;
                    firstOffset = _fieldDescriptors[_typeDescriptors[superClassRef].FieldIndex].DataOffset;
                }

                AddField("$", (uint)Fnv1.HashString("$"), 0, superClassRef, firstOffset, 0, typeDesc.FieldIndex);

                typeDesc.SetFieldCount((ushort)(typeDesc.GetFieldCount() + 1));
                typeDesc.Size = _typeDescriptors[superClassRef].Size;
                typeDesc.SetAlignment(_typeDescriptors[superClassRef].GetAlignment());
                fieldIndex++;
            }

            foreach (PropertyInfo pi in classProperties)
            {
                ProcessField(pi, ref typeDesc, typeDesc.FieldIndex + fieldIndex++);
            }

            while (typeDesc.Size % typeDesc.GetAlignment() != 0)
            {
                typeDesc.Size++;
            }

            if (inherited && hasFirstField && typeDesc.GetFieldCount() > 1)
            {
                EbxFieldDescriptor inheritedField = _fieldDescriptors[typeDesc.FieldIndex];
                inheritedField.DataOffset = _fieldDescriptors[typeDesc.FieldIndex + 1].DataOffset;
                _fieldDescriptors[typeDesc.FieldIndex] = inheritedField;
            }

            _typeDescriptors[index] = typeDesc;
        }
        else if (inType.IsValueType)
        {
            PropertyInfo[] allProps =
                inType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            // Ignore transients
            List<PropertyInfo> objProperties =
                [.. allProps.Where(static pi => pi.GetCustomAttribute<IsTransientAttribute>() is null)];

            string name = inType.GetName();

            uint nameHash = (uint)Fnv1.HashString(name);

            index = AddType(name, nameHash, _fieldDescriptors.Count, (byte)objProperties.Count, 1, typeMeta!.Flags, 0,
                0);

            EbxTypeDescriptor typeDesc = _typeDescriptors[index];

            ReserveFields(typeDesc.GetFieldCount());

            int fieldIndex = 0;
            foreach (PropertyInfo pi in objProperties)
            {
                ProcessField(pi, ref typeDesc, typeDesc.FieldIndex + fieldIndex++);
            }

            if (typeMeta.Flags.GetFlags().HasFlag(Flags.LayoutImmutable))
            {
                typeDesc.Size = typeMeta.Size;
                typeDesc.SetAlignment(typeMeta.Alignment);
            }

            while (typeDesc.Size % typeDesc.GetAlignment() != 0)
            {
                typeDesc.Size++;
            }

            _typeDescriptors[index] = typeDesc;
        }
        else
        {
            throw new NotSupportedException($"The Legacy EBX writer does not support type {inType}.");
        }

        return index;
    }

    private ushort FindOrAddType(Type inType)
    {
        int index = FindExistingType(inType);
        if (index == -1)
        {
            index = AddType(inType);
        }

        return (ushort)index;
    }

    private void ProcessField(PropertyInfo objField, ref EbxTypeDescriptor typeDesc, int fieldIndex)
    {
        ushort classRef = 0;

        EbxFieldMetaAttribute? fieldMeta = objField.GetCustomAttribute<EbxFieldMetaAttribute>();
        TypeEnum ebxType = fieldMeta!.Flags.GetTypeEnum();

        Type propType = objField.PropertyType;
        EbxTypeMetaAttribute? typeMeta = propType.GetCustomAttribute<EbxTypeMetaAttribute>();

        byte alignment = 1;
        ushort fieldSize = 0;
        if (typeMeta is not null)
        {
            alignment = typeMeta.Alignment;
            fieldSize = typeMeta.Size;
        }

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        // Only the types laid out differently from their EbxTypeMeta are listed,
        // the rest keep the size and alignment from above.
        switch (ebxType)
        {
            case TypeEnum.Class:
            case TypeEnum.CString:
            case TypeEnum.TypeRef:
            case TypeEnum.FileRef:
                fieldSize = 4;
                alignment = 4;
                break;
            case TypeEnum.Array:
            case TypeEnum.Struct:
            case TypeEnum.Enum:
                classRef = FindOrAddType(propType);
                alignment = _typeDescriptors[classRef].GetAlignment();
                fieldSize = _typeDescriptors[classRef].Size;
                break;
        }

        while (typeDesc.Size % alignment != 0)
        {
            typeDesc.Size++;
        }

        // TODO:
        // Classes doesn't seems to use the in memory offset from the TypeInfo.
        // Setting it to 0 and hoping it doesn't cause too many errors.
        AddField(objField.Name,
            (uint)Fnv1.HashString(objField.Name),
            fieldMeta.Flags,
            classRef,
            typeDesc.Size,
            objField.DeclaringType!.IsClass ? 0 : fieldMeta.Offset,
            fieldIndex);

        // Update size and alignment
        typeDesc.Size += fieldSize;
        typeDesc.SetAlignment(Math.Max(typeDesc.GetAlignment(), alignment));
    }

    private void ProcessData()
    {
        HashSet<Type> uniqueTypes = new(_sortedInstances.Count);

        _data = new Block<byte>(0);
        using (BlockAccessor writer = new(_data, true))
        {
            Type type = _sortedInstances[0].GetType();
            int typeDescriptorRef = FindExistingType(type);

            EbxInstance inst = new() { TypeDescriptorRef = (ushort)typeDescriptorRef, Count = 0, IsExported = true };

            ushort count = 0;
            _numExports++;

            foreach (IEbxInstance instance in _sortedInstances)
            {
                AssetClassGuid guid = instance.GetInstanceGuid();

                type = instance.GetType();
                typeDescriptorRef = FindExistingType(type);
                EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(typeDescriptorRef);

                uniqueTypes.Add(type);

                if (typeDescriptorRef != inst.TypeDescriptorRef || (inst.IsExported && !guid.IsExported))
                {
                    inst.Count = count;
                    _instances.Add(inst);

                    inst = new EbxInstance
                    {
                        TypeDescriptorRef = (ushort)typeDescriptorRef, IsExported = guid.IsExported
                    };
                    _numExports += (ushort)(inst.IsExported ? 1 : 0);

                    count = 0;
                }

                writer.Pad(typeDescriptor.GetAlignment());

                if (guid.IsExported)
                {
                    writer.WriteGuid(guid.ExportedGuid);
                }

                if (typeDescriptor.GetAlignment() != 0x04)
                {
                    writer.WriteUInt64(0);
                }

                WriteType(instance, typeDescriptor, writer, writer.Position - 8);
                count++;
            }

            // Add final instance
            inst.Count = count;
            _instances.Add(inst);
        }

        _uniqueClassCount = (ushort)uniqueTypes.Count;
    }

    private void ReserveFields(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _fieldDescriptors.Add(new EbxFieldDescriptor());
        }
    }

    private int AddType(string name, uint nameHash, int fieldIndex, byte fieldCount, byte alignment, ushort typeFlags,
        ushort size, ushort secondSize)
    {
        EbxTypeDescriptor typeDesc = new()
        {
            Name = name,
            NameHash = nameHash,
            FieldIndex = fieldIndex,
            Flags = typeFlags,
            Size = size,
            SecondSize = secondSize,
            Index = -1
        };

        typeDesc.SetFieldCount(fieldCount);
        typeDesc.SetAlignment(alignment);

        _typeDescriptors.Add(typeDesc);

        _typeNames.Add(name);
        _typeToDescriptor.Add(nameHash, _typeDescriptors.Count - 1);

        return _typeDescriptors.Count - 1;
    }

    private void AddField(string name, uint nameHash, TypeFlags typeFlags, ushort classRef, uint dataOffset,
        uint secondOffset, int index)
    {
        _fieldDescriptors[index] = new EbxFieldDescriptor
        {
            Name = name,
            NameHash = nameHash,
            Flags = typeFlags,
            TypeDescriptorRef = classRef,
            DataOffset = dataOffset,
            SecondOffset = secondOffset
        };
        _typeNames.Add(name);
    }

    private void WriteHeader(Guid inPartitionGuid)
    {
        _accessor.WriteInt32((int)(ProfilesLibrary.EbxVersion >= 4 ? EbxVersion.Version4 : EbxVersion.Version2));
        _accessor.WriteInt32(0x00); // stringsOffset
        _accessor.WriteInt32(0x00); // stringsAndDataLen
        _accessor.WriteInt32(_imports.Count);
        _accessor.WriteUInt16((ushort)_instances.Count);
        _accessor.WriteUInt16(_numExports);
        _accessor.WriteUInt16(_uniqueClassCount);
        _accessor.WriteUInt16((ushort)_typeDescriptors.Count);
        _accessor.WriteUInt16((ushort)_fieldDescriptors.Count);
        _accessor.WriteUInt16(0x00); // typeNamesLen
        _accessor.WriteInt32(0x00); // stringsLen
        _accessor.WriteInt32(_arrays.Count);
        _accessor.WriteInt32(0x00); // dataLen
        _accessor.WriteGuid(inPartitionGuid);
    }

    private void WriteType(object obj, EbxTypeDescriptor inTypeDescriptor, DataAccessor writer, long startPos)
    {
        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < inTypeDescriptor.GetFieldCount(); i++)
        {
            EbxFieldDescriptor field = _typeResolver.ResolveField(inTypeDescriptor.FieldIndex + i);
            PropertyInfo? ebxProperty = properties.FirstOrDefault(prop =>
            {
                if (string.IsNullOrEmpty(field.Name))
                {
                    return prop.GetCustomAttribute<NameHashAttribute>()?.Hash == field.NameHash;
                }

                return prop.GetName() == field.Name;
            });

            writer.Position = startPos + field.DataOffset;

            TypeEnum type = field.Flags.GetTypeEnum();
            if (type == TypeEnum.Void)
            {
                // Write superclass first
                WriteType(obj, _typeResolver.ResolveType(inTypeDescriptor, field.TypeDescriptorRef), writer, startPos);
                continue;
            }

            if (ebxProperty is not null)
            {
                WriteField(ebxProperty.GetValue(obj)!, inTypeDescriptor, type, field.TypeDescriptorRef, writer);
            }
        }

        writer.Position = startPos + inTypeDescriptor.Size;
    }

    private void WriteField(object ebxObj, EbxTypeDescriptor? inParentTypeDescriptor, TypeEnum ebxType,
        ushort inTypeDescriptorRef, DataAccessor writer)
    {
        switch (ebxType)
        {
            case TypeEnum.TypeRef:
                {
                    writer.WriteUInt32(AddString(TypeInfo.Version > 4
                        ? ((TypeRef)((IPrimitive)ebxObj).ToActualType()).Guid.ToString().ToUpperInvariant()
                        : (TypeRef)((IPrimitive)ebxObj).ToActualType()));
                    break;
                }
            case TypeEnum.FileRef:
                {
                    writer.WriteUInt32(AddString((FileRef)((IPrimitive)ebxObj).ToActualType()));
                    break;
                }
            case TypeEnum.CString:
                {
                    writer.WriteUInt32(AddString((string)((IPrimitive)ebxObj).ToActualType()));
                    break;
                }
            case TypeEnum.Class:
                {
                    PointerRef pointer = (PointerRef)ebxObj;
                    uint pointerIndex = 0;

                    switch (pointer.Type)
                    {
                        case PointerRefType.External:
                            pointerIndex = (uint)(_importOrderFw[pointer.External] | 0x80000000);
                            break;
                        case PointerRefType.Internal:
                            pointerIndex = (uint)(_sortedInstances.IndexOf(pointer.Internal!) + 1);
                            break;
                        case PointerRefType.Null:
                        default:
                            break;
                    }

                    writer.WriteUInt32(pointerIndex);
                }
                break;

            case TypeEnum.Struct:
                {
                    EbxTypeDescriptor structType = inParentTypeDescriptor.HasValue
                        ? _typeResolver.ResolveType(inParentTypeDescriptor.Value, inTypeDescriptorRef)
                        : _typeResolver.ResolveType(inTypeDescriptorRef);
                    writer.Pad(structType.GetAlignment());

                    WriteType(ebxObj, structType, writer, writer.Position);
                }
                break;

            case TypeEnum.Array:
                {
                    int typeDescriptorRef = FindExistingType(ebxObj.GetType());
                    int arrayIdx = 0;

                    EbxTypeDescriptor arrayTypeDescriptor = _typeResolver.ResolveType(typeDescriptorRef);
                    EbxFieldDescriptor elementFieldDescriptor =
                        _typeResolver.ResolveField(arrayTypeDescriptor.FieldIndex);

                    ebxType = elementFieldDescriptor.Flags.GetTypeEnum();

                    IList arrayObj = (IList)ebxObj;
                    int count = arrayObj.Count;

                    if (_arrays.Count == 0)
                    {
                        _arrays.Add(
                            new EbxArray { Count = 0, TypeDescriptorRef = typeDescriptorRef });
                    }

                    if (count > 0)
                    {
                        arrayIdx = _arrays.Count;
                        _arrays.Add(
                            new EbxArray { Count = count, TypeDescriptorRef = typeDescriptorRef });
                        Block<byte> data = new(0);
                        _arrayData.Add(data);
                        using BlockAccessor arrayWriter = new(data, true);
                        for (int i = 0; i < count; i++)
                        {
                            object subValue = arrayObj[i]!;

                            WriteField(subValue, arrayTypeDescriptor, ebxType, elementFieldDescriptor.TypeDescriptorRef,
                                arrayWriter);
                        }
                    }

                    writer.WriteInt32(arrayIdx);
                }
                break;

            case TypeEnum.Enum:
                writer.WriteInt32((int)ebxObj);
                break;
            case TypeEnum.Float32:
                writer.WriteSingle((float)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Float64:
                writer.WriteDouble((double)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Boolean:
                writer.WriteBoolean((bool)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Int8:
                writer.WriteSByte((sbyte)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.UInt8:
                writer.WriteByte((byte)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Int16:
                writer.WriteInt16((short)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.UInt16:
                writer.WriteUInt16((ushort)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Int32:
                writer.WriteInt32((int)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.UInt32:
                writer.WriteUInt32((uint)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Int64:
                writer.WriteInt64((long)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.UInt64:
                writer.WriteUInt64((ulong)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Guid:
                writer.WriteGuid((Guid)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.Sha1:
                writer.WriteSha1((Sha1)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.String:
                writer.WriteFixedSizedString((string)((IPrimitive)ebxObj).ToActualType(), 32);
                break;
            case TypeEnum.ResourceRef:
                writer.WriteUInt64((ResourceRef)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeEnum.BoxedValueRef:
                {
                    BoxedValueRef value = (BoxedValueRef)((IPrimitive)ebxObj).ToActualType();
                    int index = _boxedValues.Count;

                    if (value.Type == TypeEnum.Void)
                    {
                        index = -1;
                    }
                    else
                    {
                        EbxBoxedValue boxedValue = new()
                        {
                            Type = (ushort)value.Type,
                            // Need to get the correct TypeDescriptorRef
                            TypeDescriptorRef = value.Type is TypeEnum.Array or TypeEnum.Enum or TypeEnum.Struct
                                ? (ushort)FindExistingType(value.Value!.GetType())
                                : default
                        };

                        Block<byte> data = new(0);
                        _boxedValueData.Add(data);
                        using (BlockAccessor accessor = new(data, true))
                        {
                            WriteField(value.Value!, null, value.Type, boxedValue.TypeDescriptorRef, accessor);
                        }

                        _boxedValues.Add(boxedValue);
                    }

                    writer.WriteInt32(index);
                    writer.WriteUInt64(0);
                    writer.WriteUInt32(0);
                }
                break;

            case TypeEnum.Delegate:
            case TypeEnum.Void:
            case TypeEnum.DbObject:
            case TypeEnum.Function:
            case TypeEnum.Interface:
            default:
                throw new InvalidDataException($"Unknown EBX field type: {ebxType}");
        }
    }

    private new uint AddString(string inValue)
    {
        return string.IsNullOrEmpty(inValue) ? 0xFFFFFFFF : base.AddString(inValue);
    }
}