using System.Collections;
using System.Reflection;
using System.Diagnostics;
using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.TypeSdk;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.RiffEbx;

public sealed class EbxWriter : BaseEbxWriter
{
    private readonly List<EbxExtra> _arrays = [];
    private readonly List<EbxExtra> _boxedValues = [];

    private readonly EbxTypeResolver _typeResolver;

    private EbxFixup _fixup;

    [Flags]
    private enum Flags
    {
        Exported = 1 << 8,
        HasGuid = 1 << 12,
        Aggregated = 1 << 13,
        ReadOnly = 1 << 15
    }

    public EbxWriter(DataAccessor inAccessor)
        : base(inAccessor)
    {
        _fixup = new EbxFixup
        {
            TypeGuids = [],
            TypeSignatures = [],
            InstanceOffsets = [],
            PointerOffsets = [],
            ResourceRefOffsets = [],
            Imports = [],
            ImportOffsets = [],
            TypeInfoOffsets = []
        };
        _typeResolver = new EbxTypeResolver(_fixup.TypeGuids, _fixup.TypeSignatures);
    }

    protected override void InternalWriteEbx(Guid inPartitionGuid, int inExportedInstanceCount)
    {
        _fixup.PartitionGuid = inPartitionGuid;
        _fixup.ExportedInstanceCount = inExportedInstanceCount;

        using Block<byte> payload = ProcessData();

        RiffWriter riffWriter = new(_accessor);
        riffWriter.WriteHeader("RIFF", "EBX\0");

        // Align data of EBXD chunk to 16 bytes
        using Block<byte> data = new(payload.Size + 12);
        data.Shift(12);
        payload.CopyTo(data, payload.Size);
        data.ResetShift();

        riffWriter.WriteChunk("EBXD", data);

        _fixup.Imports = [.. _imports];

        _fixup.InstanceOffsets.Sort();
        _fixup.PointerOffsets.Sort();
        _fixup.ResourceRefOffsets.Sort();
        _fixup.ImportOffsets.Sort();
        _fixup.TypeInfoOffsets.Sort();

        using Block<byte> fixup = EbxFixup.WriteFixup(_fixup);
        riffWriter.WriteChunk("EFIX", fixup);

        // Arrays and boxed values are both written as one EbxExtra each
        using Block<byte> x = new(sizeof(int) * 2 + (_arrays.Count + _boxedValues.Count) *
                            (sizeof(uint) + sizeof(int) + sizeof(uint) + sizeof(ushort) + sizeof(ushort)));

        using (BlockAccessor subAccessor = new(x, true))
        {
            subAccessor.WriteInt32(_arrays.Count);
            subAccessor.WriteInt32(_boxedValues.Count);

            foreach (EbxExtra array in _arrays)
            {
                subAccessor.WriteUInt32(array.Offset);
                subAccessor.WriteInt32(array.Count);
                subAccessor.WriteUInt32(array.Hash);
                subAccessor.WriteUInt16(array.Flags);
                subAccessor.WriteUInt16(array.TypeDescriptorRef);
            }

            foreach (EbxExtra boxedValue in _boxedValues)
            {
                subAccessor.WriteUInt32(boxedValue.Offset);
                subAccessor.WriteInt32(boxedValue.Count);
                subAccessor.WriteUInt32(boxedValue.Hash);
                subAccessor.WriteUInt16(boxedValue.Flags);
                subAccessor.WriteUInt16(boxedValue.TypeDescriptorRef);
            }
        }

        riffWriter.WriteChunk("EBXX", x);

        riffWriter.Fixup();
    }

    protected override int CompareInstances(IEbxInstance inA, IEbxInstance inB)
    {
        ArgumentNullException.ThrowIfNull(inA);
        ArgumentNullException.ThrowIfNull(inB);

        Type typeA = inA.GetType();
        Type typeB = inB.GetType();

        Guid typeGuidA = typeA.GetGuid();
        Guid typeGuidB = typeB.GetGuid();

        // Sort for highest alignment first
        int a = typeB.GetCustomAttribute<EbxTypeMetaAttribute>()!.Alignment.CompareTo(
            typeA.GetCustomAttribute<EbxTypeMetaAttribute>()!.Alignment);
        if (a != 0)
        {
            return a;
        }

        // Then sort for type, and the ID
        a = typeGuidA.CompareTo(typeGuidB);
        return a != 0 ? a : inA.GetInstanceGuid().InternalId.CompareTo(inB.GetInstanceGuid().InternalId);
    }

    protected override int AddType(Type inType)
    {
        ArgumentNullException.ThrowIfNull(inType);

        if (inType.Name.Equals(CollectionName, StringComparison.Ordinal))
        {
            return -1;
        }
        return AddType(inType, true);
    }

    private Block<byte> ProcessData()
    {
        Block<byte> data = new(1);
        using BlockAccessor writer = new(data, true);
        foreach (IEbxInstance instance in _sortedInstances)
        {
            AssetClassGuid guid = instance.GetInstanceGuid();

            Type type = instance.GetType();
            int typeDescriptorRef = FindExistingType(type);
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(typeDescriptorRef);

            writer.Pad(typeDescriptor.Alignment);

            if (guid.IsExported)
            {
                writer.WriteGuid(guid.ExportedGuid);
            }
            _fixup.InstanceOffsets.Add((uint)writer.Position);
            long classStartOffset = writer.Position;

            writer.WriteInt64(typeDescriptorRef);
            if (typeDescriptor.Alignment != 0x04)
            {
                writer.WriteUInt64(0);
            }

            writer.WriteUInt32(2); // Seems to always be the same (needs more testing)

            Flags flags = Flags.Aggregated | Flags.ReadOnly;
            if (guid.IsExported)
            {
                flags |= Flags.HasGuid | Flags.Exported;
            }

            writer.WriteUInt32((uint)flags);

            WriteType(instance, _typeResolver.ResolveType(typeDescriptorRef), writer, classStartOffset);
        }

        writer.Pad(16);

        _fixup.ArrayOffset = (uint)writer.Position;
        ReadOnlySpan<byte> emptyArray = stackalloc byte[32];
        writer.Write(emptyArray);
        if (_arrayData.Count > 0)
        {
            for (int i = 0; i < _arrays.Count; i++)
            {
                EbxExtra array = _arrays[i];
                ushort alignment = GetAlignment(array);

                // Make sure the array data is padded correctly for the first item
                writer.Position += sizeof(int);
                writer.Pad(alignment);
                writer.Position -= sizeof(int);
                writer.WriteInt32(array.Count);

                array.Offset = (uint)writer.Position;
                writer.Write(_arrayData[i]);
                _arrayData[i].Dispose();
                _arrays[i] = array;
            }
            writer.Pad(16);
        }

        _fixup.BoxedValueRefOffset = (uint)writer.Position;
        if (_boxedValueData.Count > 0)
        {
            for (int i = 0; i < _boxedValues.Count; i++)
            {
                EbxExtra boxedValue = _boxedValues[i];
                ushort alignment = GetAlignment(boxedValue);

                // Make sure the array data is padded correctly for the first item
                writer.Pad(alignment);

                boxedValue.Offset = (uint)writer.Position;
                writer.Write(_boxedValueData[i]);
                _boxedValueData[i].Dispose();
                _boxedValues[i] = boxedValue;
            }
        }

        _fixup.StringOffset = (uint)writer.Position;
        foreach (string str in _strings)
        {
            writer.WriteNullTerminatedString(str);
        }

        FixupPointers(writer);

        return data;
    }

    private ushort GetAlignment(EbxExtra inExtra)
    {
        TypeFlags.TypeEnum type = inExtra.Flags.GetTypeEnum();

        return type switch
        {
            TypeFlags.TypeEnum.Struct => _typeResolver.ResolveType(inExtra.TypeDescriptorRef).Alignment,
            TypeFlags.TypeEnum.Float64 or TypeFlags.TypeEnum.Int64 or TypeFlags.TypeEnum.UInt64
                or TypeFlags.TypeEnum.CString or TypeFlags.TypeEnum.FileRef or TypeFlags.TypeEnum.Delegate
                or TypeFlags.TypeEnum.Class or TypeFlags.TypeEnum.ResourceRef or TypeFlags.TypeEnum.BoxedValueRef => 8,
            _ => 4
        };
    }

    private int AddType(Type inType, bool inAddSignature)
    {
        _typeToDescriptor.Add(inType.GetNameHash(), _fixup.TypeGuids.Count);

        _fixup.TypeGuids.Add(inType.GetGuid());

        if (inAddSignature)
        {
            _fixup.TypeSignatures.Add(inType.GetSignature());
        }

        return _fixup.TypeGuids.Count - 1;
    }

    private int AddType(IType inType, bool inAddSignature)
    {
        _typeToDescriptor.Add(inType.NameHash, _fixup.TypeGuids.Count);

        _fixup.TypeGuids.Add(inType.Guid);

        if (inAddSignature)
        {
            _fixup.TypeSignatures.Add(inType.Signature);
        }

        return _fixup.TypeGuids.Count - 1;
    }

    private void WriteType(object obj, EbxTypeDescriptor type, DataAccessor writer, long startPos)
    {
        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < type.FieldCount; i++)
        {
            EbxFieldDescriptor field = EbxTypeResolver.ResolveField(type.FieldIndex + i);
            PropertyInfo? ebxProperty = properties.FirstOrDefault(p => p.GetNameHash() == field.NameHash);

            writer.Position = startPos + field.DataOffset;

            bool isArray = field.Flags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array;
            if (!isArray && field.Flags.GetTypeEnum() == TypeFlags.TypeEnum.Void)
            {
                WriteType(obj, EbxTypeResolver.ResolveTypeFromField(field.TypeDescriptorRef), writer, startPos);
                continue;
            }

            if (ebxProperty is null)
            {
                continue;
            }

            if (isArray)
            {
                WriteArray(ebxProperty.GetValue(obj)!, field, writer);
            }
            else
            {
                WriteField(ebxProperty.GetValue(obj)!, field, writer);
            }
        }

        writer.Position = startPos + type.Size;
    }

    private void SharedWriteField(object ebxObj,
        TypeFlags.TypeEnum ebxType,
        DataAccessor writer)
    {
        switch (ebxType)
        {
            case TypeFlags.TypeEnum.TypeRef:
                WriteTypeRef((TypeRef)((IPrimitive)ebxObj).ToActualType(), writer, false);
                break;
            case TypeFlags.TypeEnum.FileRef:
                writer.WriteUInt64(AddString((FileRef)((IPrimitive)ebxObj).ToActualType()));
                break;
            case TypeFlags.TypeEnum.CString:
                writer.WriteUInt64(AddString((string)((IPrimitive)ebxObj).ToActualType()));
                break;
            case TypeFlags.TypeEnum.Class:
            {
                PointerRef pointer = (PointerRef)ebxObj;
                ulong pointerIndex = 0;

                switch (pointer.Type)
                {
                    case PointerRefType.External:
                        // The import list in the EBX counts both GUIDs of each import separately.
                        // What we want is the index to the class GUID.
                        pointerIndex = (ulong)((_importOrderFw[pointer.External] * 2) + 1);
                        break;
                    case PointerRefType.Internal:
                        pointerIndex = (ulong)_sortedInstances.IndexOf(pointer.Internal!);
                        break;
                    case PointerRefType.Null:
                    default:
                        break;
                }

                writer.WriteUInt64(pointerIndex);
                break;
            }
            case TypeFlags.TypeEnum.Enum:
                writer.WriteInt32((int)ebxObj);
                break;
            case TypeFlags.TypeEnum.Float32:
                writer.WriteSingle((float)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Float64:
                writer.WriteDouble((double)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Boolean:
                writer.WriteBoolean((bool)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Int8:
                writer.WriteSByte((sbyte)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.UInt8:
                writer.WriteByte((byte)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Int16:
                writer.WriteInt16((short)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.UInt16:
                writer.WriteUInt16((ushort)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Int32:
                writer.WriteInt32((int)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.UInt32:
                writer.WriteUInt32((uint)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Int64:
                writer.WriteInt64((long)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.UInt64:
                writer.WriteUInt64((ulong)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Guid:
                writer.WriteGuid((Guid)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.Sha1:
                writer.WriteSha1((Sha1)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.String:
                writer.WriteFixedSizedString((string)((IPrimitive)ebxObj).ToActualType(), 32);
                break;
            case TypeFlags.TypeEnum.ResourceRef:
                writer.WriteUInt64((ResourceRef)((IPrimitive)ebxObj).ToActualType());
                break;
            case TypeFlags.TypeEnum.BoxedValueRef:
                WriteBoxedValueRef((BoxedValueRef)((IPrimitive)ebxObj).ToActualType(), writer);
                break;
            case TypeFlags.TypeEnum.Struct:
                throw new UnreachableException("Structs are written by WriteField and WriteBoxedField.");
            case TypeFlags.TypeEnum.Array:
                throw new NotImplementedException("Array");
            case TypeFlags.TypeEnum.DbObject:
                throw new NotImplementedException("DbObject");
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
            case TypeFlags.TypeEnum.Delegate:
            default:
                throw new InvalidDataException($"Unknown EBX field type {ebxType}.");
        }
    }

    private void WriteField(object ebxObj, EbxFieldDescriptor inFieldDescriptor, DataAccessor writer)
    {
        TypeFlags.TypeEnum type = inFieldDescriptor.Flags.GetTypeEnum();
        if (type == TypeFlags.TypeEnum.Struct)
        {
            EbxTypeDescriptor typeDescriptor = EbxTypeResolver.ResolveTypeFromField(inFieldDescriptor.TypeDescriptorRef);
            writer.Pad(typeDescriptor.Alignment);
            WriteType(ebxObj, typeDescriptor, writer, writer.Position);
            return;
        }
        
        SharedWriteField(ebxObj, type, writer);
    }

    private void WriteBoxedField(object ebxObj, TypeFlags.TypeEnum inType, int inTypeDescriptorRef, DataAccessor writer)
    {
        if (inType != TypeFlags.TypeEnum.Struct)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(inTypeDescriptorRef);
            writer.Pad(typeDescriptor.Alignment);
            WriteType(ebxObj, typeDescriptor, writer, writer.Position);
            return;
        }
        SharedWriteField(ebxObj, inType, writer);
    }

    private void WriteBoxedValueRef(BoxedValueRef inBoxedValueRef, DataAccessor inWriter)
    {
        Type? type = inBoxedValueRef.Value?.GetType();
        TypeRef typeRef = type is null ? new TypeRef() : new TypeRef(new SdkType(type));
        (TypeFlags Flags, ushort TypeDescriptorRef) tiPair = WriteTypeRef(typeRef, inWriter, true);

        int index = _boxedValues.Count;
        if (inBoxedValueRef.Value is not null)
        {
            EbxExtra boxedValue = new()
            {
                Count = 1,
                Flags = tiPair.Flags,
                TypeDescriptorRef = tiPair.TypeDescriptorRef
            };
            _boxedValues.Add(boxedValue);

            Block<byte> data = new(0);
            _boxedValueData.Add(data);
            using BlockAccessor accessor = new(data, true);
            if (boxedValue.Flags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array)
            {
                WriteBoxedArray(inBoxedValueRef.Value, inBoxedValueRef.Type, boxedValue.TypeDescriptorRef, accessor);
            }
            else
            {
                WriteBoxedField(inBoxedValueRef.Value, inBoxedValueRef.Type, boxedValue.TypeDescriptorRef, accessor);
            }
        }
        else
        {
            index = 0;
        }

        inWriter.WriteInt64(index);
    }

    private (TypeFlags, ushort) WriteTypeRef(TypeRef typeRef, DataAccessor writer, bool inAddSignature)
    {
        if (typeRef.IsNull())
        {
            writer.WriteInt32(0x00);
            writer.WriteInt32(0x00);
            return (0, 0);
        }

        ushort typeIdx;

        TypeFlags typeFlags = typeRef.InternalType!.GetFlags();
        if (typeRef.InternalType is not TypeInfoAsset &&
            (typeRef.Type!.IsAssignableTo(typeof(IPrimitive)) || typeRef.Type == PointerType))
        {
            typeIdx = ushort.MaxValue;
            writer.WriteUInt32(typeFlags | 0x80000000);
            writer.WriteInt32(-1);
        }
        else
        {
            typeIdx = (ushort)FindExistingType(typeRef.InternalType!);
            if (typeIdx == ushort.MaxValue)
            {
                // Boxed value type refs shouldn't end up here,
                // as they're already handled when processing classes
                typeIdx = (ushort)AddType(typeRef.InternalType!, inAddSignature);
            }

            writer.WriteUInt32((uint)(typeIdx << 2) | 2);
            writer.WriteInt32(0);
        }

        return (typeFlags, typeIdx);
    }

    private void WriteArray(object inObj, EbxFieldDescriptor inFieldDescriptor, DataAccessor writer)
    {
        // Cast to IList to avoid invoking methods manually
        IList arrayObj = (IList)inObj;
        int count = arrayObj.Count;
        int arrayIdx = 0;

        if (count > 0)
        {
            arrayIdx = _arrays.Count;
            _arrays.Add(
                new EbxExtra
                {
                    Count = count,
                    TypeDescriptorRef = (ushort)FindExistingType(inObj.GetType().GenericTypeArguments[0]),
                    Flags = inFieldDescriptor.Flags
                });

            Block<byte> data = new(0);
            _arrayData.Add(data);
            using BlockAccessor arrayWriter = new(data, true);
            for (int i = 0; i < count; i++)
            {
                object subValue = arrayObj[i]!;

                WriteField(subValue, inFieldDescriptor, arrayWriter);
            }
        }
        writer.WriteInt64(arrayIdx);
    }

    private void WriteBoxedArray(object inObj, TypeFlags.TypeEnum inType, int inTypeDescriptorRef, DataAccessor writer)
    {
        // Cast to IList to avoid having to invoke methods manually
        IList arrayObj = (IList)inObj;
        int count = arrayObj.Count;
        int arrayIdx = 0;

        if (count > 0)
        {
            arrayIdx = _arrays.Count;
            _arrays.Add(
                new EbxExtra
                {
                    Count = count,
                    TypeDescriptorRef = (ushort)FindExistingType(inObj.GetType().GenericTypeArguments[0]),
                    Flags = new TypeFlags(inType, TypeFlags.CategoryEnum.Array, unk: 0)
                });

            Block<byte> data = new(0);
            _arrayData.Add(data);
            using BlockAccessor arrayWriter = new(data, true);
            for (int i = 0; i < count; i++)
            {
                object subValue = arrayObj[i]!;

                WriteBoxedField(subValue, inType, inTypeDescriptorRef, arrayWriter);
            }
        }
        writer.WriteInt64(arrayIdx);
    }

    private void FixupPointers(DataAccessor writer)
    {
        for (int i = 0; i < _sortedInstances.Count; i++)
        {
            Type type = _sortedInstances[i].GetType();
            int classIdx = FindExistingType(type);
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(classIdx);

            writer.Position = _fixup.InstanceOffsets[i];

            writer.Position += sizeof(long);
            if (typeDescriptor.Alignment != 0x04)
            {
                writer.Position += sizeof(long);
            }

            writer.Position += sizeof(int);
            writer.Position += sizeof(int);

            FixupType(_sortedInstances[i], typeDescriptor, writer, _fixup.InstanceOffsets[i]);
        }
    }

    private void FixupType(object obj, EbxTypeDescriptor type, DataAccessor writer, long startPos)
    {
        // Sweep through the EBX data after writing it to patch pointers/offsets
        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < type.FieldCount; i++)
        {
            EbxFieldDescriptor field = EbxTypeResolver.ResolveField(type.FieldIndex + i);
            PropertyInfo? ebxProperty = properties.FirstOrDefault(p => p.GetNameHash() == field.NameHash);

            writer.Position = startPos + field.DataOffset;

            bool isArray = field.Flags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array;
            if (!isArray && field.Flags.GetTypeEnum() == TypeFlags.TypeEnum.Void)
            {
                FixupType(obj, EbxTypeResolver.ResolveTypeFromField(field.TypeDescriptorRef), writer, startPos);
                continue;
            }

            if (ebxProperty is null)
            {
                continue;
            }

            if (isArray)
            {
                FixupArray(ebxProperty.GetValue(obj)!, field, writer);
            }
            else
            {
                FixupField(ebxProperty.GetValue(obj)!, field, writer);
            }
        }

        writer.Position = startPos + type.Size;
    }

    private void SharedFixupField(object obj, TypeFlags.TypeEnum inType, DataAccessor writer)
    {
        switch (inType)
        {
            case TypeFlags.TypeEnum.TypeRef:
                FixupTypeRef(writer);
                break;
            case TypeFlags.TypeEnum.FileRef:
            case TypeFlags.TypeEnum.CString:
                FixupPointer(_fixup.StringOffset, writer);
                break;
            case TypeFlags.TypeEnum.Class:
                switch (((PointerRef)obj).Type)
                {
                    case PointerRefType.External:
                        _fixup.ImportOffsets.Add((uint)writer.Position);
                        writer.Position += sizeof(long);
                        break;
                    case PointerRefType.Internal:
                        FixupInternalRef(writer);
                        break;
                    case PointerRefType.Null:
                        writer.Position += sizeof(long);
                        break;
                    default:
                        throw new InvalidDataException($"Unknown pointer type: {((PointerRef)obj).Type}");
                }

                break;
            case TypeFlags.TypeEnum.ResourceRef:
                FixupResourceRef(writer);
                break;
            case TypeFlags.TypeEnum.BoxedValueRef:
                FixupTypeRef(writer);
                FixupBoxedValue(obj, writer);
                break;
            case TypeFlags.TypeEnum.Enum:
                writer.Position += sizeof(int);
                break;
            case TypeFlags.TypeEnum.Float32:
                writer.Position += sizeof(float);
                break;
            case TypeFlags.TypeEnum.Float64:
                writer.Position += sizeof(double);
                break;
            case TypeFlags.TypeEnum.Boolean:
                writer.Position += sizeof(bool);
                break;
            case TypeFlags.TypeEnum.Int8:
            case TypeFlags.TypeEnum.UInt8:
                writer.Position += sizeof(byte);
                break;
            case TypeFlags.TypeEnum.Int16:
            case TypeFlags.TypeEnum.UInt16:
                writer.Position += sizeof(short);
                break;
            case TypeFlags.TypeEnum.Int32:
            case TypeFlags.TypeEnum.UInt32:
                writer.Position += sizeof(int);
                break;
            case TypeFlags.TypeEnum.Int64:
            case TypeFlags.TypeEnum.UInt64:
                writer.Position += sizeof(long);
                break;
            case TypeFlags.TypeEnum.Guid:
                writer.Position += 16;
                break;
            case TypeFlags.TypeEnum.Sha1:
                writer.Position += 20;
                break;
            case TypeFlags.TypeEnum.String:
                writer.Position += 32;
                break;
            case TypeFlags.TypeEnum.Struct:
                throw new UnreachableException("Structs are fixed up by FixupField and FixupBoxedField.");
            case TypeFlags.TypeEnum.Array:
                throw new NotImplementedException("Array");
            case TypeFlags.TypeEnum.DbObject:
                throw new NotImplementedException("DbObject");
            case TypeFlags.TypeEnum.Delegate:
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
            default:
                throw new InvalidDataException($"Unknown EBX field type {inType}.");
        }
    }

    private void FixupField(object obj, EbxFieldDescriptor inFieldDescriptor, DataAccessor writer)
    {
        TypeFlags.TypeEnum type = inFieldDescriptor.Flags.GetTypeEnum();
        if (type != TypeFlags.TypeEnum.Struct)
        {
            EbxTypeDescriptor typeDescriptor = EbxTypeResolver.ResolveTypeFromField(inFieldDescriptor.TypeDescriptorRef);
            writer.Pad(typeDescriptor.Alignment);
            FixupType(obj, typeDescriptor, writer, writer.Position);
            return;
        }
        SharedFixupField(obj, type, writer);
    }

    private void FixupBoxedField(object obj, TypeFlags.TypeEnum inType, int inTypeDescriptorRef, DataAccessor writer)
    {
        if (inType != TypeFlags.TypeEnum.Struct)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(inTypeDescriptorRef);
            writer.Pad(typeDescriptor.Alignment);
            FixupType(obj, typeDescriptor, writer, writer.Position);
            return;
        }
        SharedFixupField(obj, inType, writer);
    }

    private void FixupPointer(long sectionOffset, DataAccessor writer)
    {
        long fieldOffset = writer.Position;
        _fixup.PointerOffsets.Add((uint)fieldOffset);
        long offset = sectionOffset + writer.ReadInt64();
        writer.Position = fieldOffset;
        offset -= writer.Position;
        // File pointers only use 32 bits,
        // but runtime pointers need 64 bits when being patched.
        // Cast down and zero out the other 32 bits
        writer.WriteInt32((int)offset);
        writer.WriteInt32(0);
    }

    private void FixupInternalRef(DataAccessor writer)
    {
        long fieldOffset = writer.Position;
        _fixup.PointerOffsets.Add((uint)fieldOffset);

        int refIdx = (int)writer.ReadInt64();
        writer.Position = fieldOffset;

        long offset = _fixup.InstanceOffsets[refIdx];
        offset -= writer.Position;
        // File pointers only use 32 bits,
        // but runtime pointers need 64 bits when being patched.
        // Cast down and zero out the other 32 bits
        writer.WriteInt32((int)offset);
        writer.WriteInt32(0);
    }

    private void FixupArray(object obj, EbxFieldDescriptor inFieldDescriptor, DataAccessor writer)
    {
        // Array pointers need to be in the pointer list
        long fieldOffset = writer.Position;
        _fixup.PointerOffsets.Add((uint)fieldOffset);
        int arrayIdx = (int)writer.ReadInt64();
        writer.Position = fieldOffset;

        IList arrayObj = (IList)obj;
        if (arrayObj.Count == 0)
        {
            // Arrays with zero elements always point to an empty value 16 bytes into the array section
            long offset = _fixup.ArrayOffset + 0x10;
            offset -= writer.Position;
            writer.WriteInt32((int)offset);
            writer.WriteInt32(0);
        }
        else
        {
            EbxExtra array = _arrays[arrayIdx];
            long offset = array.Offset;
            offset -= writer.Position;
            // File pointers only use 32 bits,
            // but runtime pointers need 64 bits when being patched.
            // Cast down and zero out the other 32 bits
            writer.WriteInt32((int)offset);
            writer.WriteInt32(0);

            writer.StepIn(array.Offset);
            for (int i = 0; i < array.Count; i++)
            {
                object subValue = arrayObj[i]!;
                FixupField(subValue, inFieldDescriptor, writer);
            }
            writer.StepOut();
        }
    }

    private void FixupBoxedArray(object obj, TypeFlags.TypeEnum inType, int inTypeDescriptorRef, DataAccessor writer)
    {
        // Array pointers need to be in the pointer list
        long fieldOffset = writer.Position;
        _fixup.PointerOffsets.Add((uint)fieldOffset);
        int arrayIdx = (int)writer.ReadInt64();
        writer.Position = fieldOffset;

        IList arrayObj = (IList)obj;
        if (arrayObj.Count == 0)
        {
            // Arrays with zero elements always point to an empty value 16 bytes into the array section
            long offset = _fixup.ArrayOffset + 0x10;
            offset -= writer.Position;
            writer.WriteInt32((int)offset);
            writer.WriteInt32(0);
        }
        else
        {
            EbxExtra array = _arrays[arrayIdx];
            long offset = array.Offset;
            offset -= writer.Position;
            // File pointers only use 32 bits,
            // but runtime pointers need 64 bits when being patched.
            // Cast down and zero out the other 32 bits
            writer.WriteInt32((int)offset);
            writer.WriteInt32(0);

            writer.StepIn(array.Offset);
            for (int i = 0; i < array.Count; i++)
            {
                object subValue = arrayObj[i]!;
                FixupBoxedField(subValue, inType, inTypeDescriptorRef, writer);
            }
            writer.StepOut();
        }
    }

    private void FixupTypeRef(DataAccessor writer)
    {
        long fieldOffset = writer.Position;
        uint type = writer.ReadUInt32();
        int typeRef = writer.ReadInt32();

        if (type == 0 && typeRef == 0)
        {
            return;
        }

        _fixup.TypeInfoOffsets.Add((uint)fieldOffset);
    }

    private void FixupResourceRef(DataAccessor writer)
    {
        long fieldOffset = writer.Position;
        ulong resourceRef = writer.ReadUInt64();

        if (resourceRef == 0)
        {
            return;
        }

        _fixup.ResourceRefOffsets.Add((uint)fieldOffset);
    }

    private void FixupBoxedValue(object obj, DataAccessor writer)
    {
        long fieldOffset = writer.Position;
        BoxedValueRef value = (BoxedValueRef)((IPrimitive)obj).ToActualType();

        int boxedValIdx = (int)writer.ReadInt64();
        writer.Position = fieldOffset;
        // Null-boxed values always have an offset of zero
        if (value.Value is null)
        {
            writer.WriteUInt64(0);
            return;
        }

        // Boxed value pointers need to be in the pointer list
        _fixup.PointerOffsets.Add((uint)fieldOffset);
        EbxExtra boxedVal = _boxedValues[boxedValIdx];

        long offset = boxedVal.Offset;
        offset -= writer.Position;
        // File pointers only use 32 bits,
        // but runtime pointers need 64 bits when being patched.
        // Cast down and zero out the other 32 bits
        writer.WriteUInt32((uint)offset);
        writer.WriteInt32(0);

        writer.StepIn(boxedVal.Offset);

        if (boxedVal.Flags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array)
        {
            FixupBoxedArray(value.Value, boxedVal.Flags.GetTypeEnum(), boxedVal.TypeDescriptorRef, writer);
        }
        else
        {
            FixupBoxedField(value.Value, boxedVal.Flags.GetTypeEnum(), boxedVal.TypeDescriptorRef, writer);
        }

        writer.StepOut();
    }
}