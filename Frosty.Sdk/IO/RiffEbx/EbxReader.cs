using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.TypeSdk;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.RiffEbx;

public sealed class EbxReader : BaseEbxReader
{
    private const string CollectionName = "ObservableCollection`1";

    private readonly Dictionary<uint, EbxExtra> _boxedValues = new();
    private readonly EbxTypeResolver _typeResolver;
    private EbxFixup _fixup;
    private long _payloadOffset;

    public EbxReader(DataAccessor inAccessor)
        : base(inAccessor)
    {
        RiffReader riffReader = new(_accessor);
        riffReader.ReadHeader(out FourCc fourCc, out uint size);

        if (fourCc != "EBX\0" && fourCc != "EBXS")
        {
            throw new InvalidDataException("Not a valid ebx chunk");
        }

        // Read EBXD chunk
        riffReader.ReadNextChunk(ref size, ProcessChunk);
        // Read EFIX chunk
        riffReader.ReadNextChunk(ref size, ProcessChunk);

        _typeResolver = new EbxTypeResolver(_fixup.TypeGuids, _fixup.TypeSignatures);

        // Read EBXX chunk
        riffReader.ReadNextChunk(ref size, ProcessChunk);

        Debug.Assert(riffReader.Eof);
    }

    public override Guid GetPartitionGuid()
    {
        return _fixup.PartitionGuid;
    }

    public override string GetRootType()
    {
        _accessor.Position = _payloadOffset + _fixup.InstanceOffsets[0];
        return TypeLibrary.GetType(_fixup.TypeGuids[_accessor.ReadUInt16()])?.Name ??
               throw new InvalidDataException("Unknown EBX root type.");
    }

    public override HashSet<Guid> GetDependencies()
    {
        return _fixup.Dependencies;
    }

    private void ProcessChunk(DataAccessor inAccessor, FourCc inFourCc, uint inSize)
    {
        switch ((string)inFourCc)
        {
            case "EBXD":
                ReadDataChunk(inAccessor);
                break;
            case "EFIX":
                ReadFixupChunk(inAccessor);
                break;
            case "EBXX":
                ReadXChunk(inAccessor);
                break;
        }
    }

    protected override void InternalReadObjects()
    {
        int[] typeRefs = new int[_fixup.InstanceOffsets.Count];
        for (int i = 0; i < typeRefs.Length; i++)
        {
            _accessor.Position = _payloadOffset + _fixup.InstanceOffsets[i];
            typeRefs[i] = _accessor.ReadUInt16();

            _instances.Add((IEbxInstance?)TypeLibrary.CreateObject(_typeResolver.ResolveType(typeRefs[i]).NameHash));
            _refCounts.Add(0);
        }

        _accessor.Position = _payloadOffset;

        for (int i = 0; i < _fixup.InstanceOffsets.Count; i++)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(typeRefs[i]);

            _accessor.Pad(typeDescriptor.Alignment);

            Guid instanceGuid = i < _fixup.ExportedInstanceCount ? _accessor.ReadGuid() : Guid.Empty;

            Debug.Assert(_accessor.Position - _payloadOffset == _fixup.InstanceOffsets[i]);

            IEbxInstance? obj = _instances[i];
            obj?.SetInstanceGuid(new AssetClassGuid(instanceGuid, i));
            ReadType(typeDescriptor, obj, _accessor.Position);
        }
    }

    private void ReadFixupChunk(DataAccessor inAccessor)
    {
        _fixup = EbxFixup.ReadFixup(inAccessor);
    }

    private void ReadDataChunk(DataAccessor inAccessor)
    {
        inAccessor.Pad(16);
        _payloadOffset = inAccessor.Position;
    }

    private void ReadXChunk(DataAccessor inAccessor)
    {
        int arrayCount = inAccessor.ReadInt32();
        int boxedValueCount = inAccessor.ReadInt32();
        _boxedValues.EnsureCapacity(boxedValueCount);

        inAccessor.Position +=
            arrayCount * (sizeof(uint) + sizeof(int) + sizeof(uint) + sizeof(ushort) + sizeof(ushort));

        for (int i = 0; i < boxedValueCount; i++)
        {
            EbxExtra b = new()
            {
                Offset = inAccessor.ReadUInt32(),
                Count = inAccessor.ReadInt32(),
                Hash = inAccessor.ReadUInt32(),
                Flags = inAccessor.ReadUInt16(),
                TypeDescriptorRef = inAccessor.ReadUInt16()
            };

            _boxedValues.Add(b.Offset, b);
        }
    }

    private void ReadType(EbxTypeDescriptor inTypeDescriptor, object? obj, long inStartOffset)
    {
        if (obj is null)
        {
            _accessor.Position = inStartOffset + inTypeDescriptor.Size;
            _accessor.Pad(inTypeDescriptor.Alignment);
            return;
        }

        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties();

        for (int i = 0; i < inTypeDescriptor.FieldCount; i++)
        {
            EbxFieldDescriptor fieldDescriptor = EbxTypeResolver.ResolveField(inTypeDescriptor.FieldIndex + i);
            PropertyInfo? propertyInfo = properties.FirstOrDefault(prop =>
                prop.GetCustomAttribute<NameHashAttribute>()?.Hash == fieldDescriptor.NameHash);

            _accessor.Position = inStartOffset + fieldDescriptor.DataOffset;

            TypeFlags.TypeEnum type = fieldDescriptor.Flags.GetTypeEnum();

            if (fieldDescriptor.Flags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array)
            {
                if (propertyInfo is null)
                {
                    //FrostyLogger.Logger.LogDebug("Skipping field \"{}.{}\", because it does not exist in the type info", objType.GetName(), fieldDescriptor.NameHash);
                    continue;
                }

                IList? list = (IList?)propertyInfo.GetValue(obj);
                Type elementType = propertyInfo.PropertyType.GenericTypeArguments[0];
                ReadArray(fieldDescriptor, value =>
                {
                    if (TryWrapValue(elementType, ref value))
                    {
                        list?.Add(value);
                    }
                });
            }
            else
            {
                if (type == TypeFlags.TypeEnum.Void)
                {
                    ReadType(EbxTypeResolver.ResolveTypeFromField(fieldDescriptor.TypeDescriptorRef), obj,
                        inStartOffset);
                    continue;
                }

                if (propertyInfo is null)
                {
                    //FrostyLogger.Logger.LogDebug("Skipping field \"{}.{}\", because it does not exist in the type info", objType.GetName(), fieldDescriptor.NameHash);
                    continue;
                }

                ReadField(fieldDescriptor, value =>
                {
                    if (TryWrapValue(propertyInfo.PropertyType, ref value))
                    {
                        propertyInfo.SetValue(obj, value);
                    }
                });
            }
        }

        // Ordering of fields is weird
        _accessor.Position = inStartOffset + inTypeDescriptor.Size;
    }

    private void SharedReadField(TypeFlags.TypeEnum inType, Action<object?> inAddFunc)
    {
        switch (inType)
        {
            case TypeFlags.TypeEnum.Boolean:
                inAddFunc(_accessor.ReadBoolean());
                break;
            case TypeFlags.TypeEnum.Int8:
                inAddFunc((sbyte)_accessor.ReadByte());
                break;
            case TypeFlags.TypeEnum.UInt8:
                inAddFunc(_accessor.ReadByte());
                break;
            case TypeFlags.TypeEnum.Int16:
                inAddFunc(_accessor.ReadInt16());
                break;
            case TypeFlags.TypeEnum.UInt16:
                inAddFunc(_accessor.ReadUInt16());
                break;
            case TypeFlags.TypeEnum.Int32:
                inAddFunc(_accessor.ReadInt32());
                break;
            case TypeFlags.TypeEnum.UInt32:
                inAddFunc(_accessor.ReadUInt32());
                break;
            case TypeFlags.TypeEnum.Int64:
                inAddFunc(_accessor.ReadInt64());
                break;
            case TypeFlags.TypeEnum.UInt64:
                inAddFunc(_accessor.ReadUInt64());
                break;
            case TypeFlags.TypeEnum.Float32:
                inAddFunc(_accessor.ReadSingle());
                break;
            case TypeFlags.TypeEnum.Float64:
                inAddFunc(_accessor.ReadDouble());
                break;
            case TypeFlags.TypeEnum.Guid:
                inAddFunc(_accessor.ReadGuid());
                break;
            case TypeFlags.TypeEnum.ResourceRef:
                inAddFunc(ReadResourceRef());
                break;
            case TypeFlags.TypeEnum.Sha1:
                inAddFunc(_accessor.ReadSha1());
                break;
            case TypeFlags.TypeEnum.String:
                inAddFunc(_accessor.ReadFixedSizedString(32));
                break;
            case TypeFlags.TypeEnum.CString:
                inAddFunc(ReadString(_accessor.ReadInt64()));
                break;
            case TypeFlags.TypeEnum.FileRef:
                inAddFunc(ReadFileRef());
                break;
            case TypeFlags.TypeEnum.Delegate:
                inAddFunc(ReadDelegate());
                break;
            case TypeFlags.TypeEnum.TypeRef:
                inAddFunc(ReadTypeRef());
                break;
            case TypeFlags.TypeEnum.BoxedValueRef:
                inAddFunc(ReadBoxedValueRef());
                break;
            case TypeFlags.TypeEnum.Enum:
                inAddFunc(_accessor.ReadInt32());
                break;
            case TypeFlags.TypeEnum.Class:
                inAddFunc(ReadPointerRef());
                break;
            case TypeFlags.TypeEnum.Struct:
                throw new UnreachableException("Structs are read by ReadField and ReadBoxedField.");
            case TypeFlags.TypeEnum.Array:
                throw new NotImplementedException("Array");
            case TypeFlags.TypeEnum.DbObject:
                throw new NotImplementedException("DbObject");
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
            default:
                throw new InvalidDataException($"Unknown EBX field type: {inType}");
        }
    }

    private void ReadField(EbxFieldDescriptor inFieldDescriptor, Action<object?> inAddFunc)
    {
        TypeFlags.TypeEnum type = inFieldDescriptor.Flags.GetTypeEnum();
        if (type != TypeFlags.TypeEnum.Struct)
        {
            SharedReadField(type, inAddFunc);
            return;
        }

        EbxTypeDescriptor structType = EbxTypeResolver.ResolveTypeFromField(inFieldDescriptor.TypeDescriptorRef);
        _accessor.Pad(structType.Alignment);
        object? obj = TypeLibrary.CreateObject(structType.NameHash);
        ReadType(structType, obj, _accessor.Position);
        inAddFunc(obj);
    }

    private void ReadBoxedField(TypeFlags.TypeEnum inType,
        int inTypeDescriptorRef, Action<object?> inAddFunc)
    {
        if (inType != TypeFlags.TypeEnum.Struct)
        {
            SharedReadField(inType, inAddFunc);
            return;
        }

        EbxTypeDescriptor structType = _typeResolver.ResolveType(inTypeDescriptorRef);
        _accessor.Pad(structType.Alignment);
        object? obj = TypeLibrary.CreateObject(structType.NameHash);
        ReadType(structType, obj, _accessor.Position);
        inAddFunc(obj);
    }

    private void ReadArray(EbxFieldDescriptor inFieldDescriptor,
        Action<object?> inAddFunc)
    {
        int offset = _accessor.ReadInt32();

        long arrayPos = _accessor.Position;

        _accessor.Position += offset - 8;

        int count = _accessor.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            ReadField(inFieldDescriptor, inAddFunc);
        }

        _accessor.Position = arrayPos;
    }

    private void ReadBoxedArray(TypeFlags.TypeEnum inType, int inTypeDescriptorRef,
        Action<object?> inAddFunc)
    {
        int offset = _accessor.ReadInt32();

        long arrayPos = _accessor.Position;

        _accessor.Position += offset - 8;

        int count = _accessor.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            ReadBoxedField(inType, inTypeDescriptorRef, inAddFunc);
        }

        _accessor.Position = arrayPos;
    }

    /// <remarks><b>Encoding:</b> UTF-8</remarks>
    private string ReadString(long offset)
    {
        if (offset == -1)
        {
            return string.Empty;
        }

        long pos = _accessor.Position;
        _accessor.Position += offset - 8;

        string retStr = _accessor.ReadNullTerminatedString();
        _accessor.Position = pos;

        return retStr;
    }

    private ResourceRef ReadResourceRef()
    {
        return new ResourceRef(_accessor.ReadUInt64());
    }

    private FileRef ReadFileRef()
    {
        return new FileRef(ReadString(_accessor.ReadInt64()));
    }

    private PointerRef ReadPointerRef()
    {
        int index = (int)_accessor.ReadInt64();

        if (index == 0)
        {
            return new PointerRef();
        }

        if ((index & 1) == 1)
        {
            return new PointerRef(_fixup.Imports[index >> 1]);
        }

        long instanceOffset = _accessor.Position - 8 + index - _payloadOffset;
        int objIndex = _fixup.InstanceMapping[(uint)instanceOffset];

        IEbxInstance? obj = _instances[objIndex];
        if (obj is null)
        {
            return new PointerRef();
        }

        _refCounts[objIndex]++;
        return new PointerRef(obj);
    }

    private TypeRef ReadTypeRef()
    {
        uint packed = _accessor.ReadUInt32();
        _accessor.Position += 4; // 0 (index in packed) or -1 (primitive)

        if (packed == 0)
        {
            return new TypeRef();
        }

        if ((packed & 0x80000000) != 0)
        {
            // Primitive type
            return new TypeRef(GetPrimitiveTypeFromEbxField((TypeFlags)(packed & ~0x80000000)));
        }

        int typeRef = (int)(packed >> 2);
        return new TypeRef(_fixup.TypeGuids[typeRef]);
    }

    private IType? ReadDelegate()
    {
        uint packed = _accessor.ReadUInt32();
        _accessor.Position += 4;

        if (packed == 0)
        {
            return null;
        }

        if ((packed & 0x80000000) != 0)
        {
            // Primitive type
            return GetPrimitiveTypeFromEbxField((TypeFlags)(packed & ~0x80000000));
        }

        int typeRef = (int)(packed >> 2);
        return TypeLibrary.GetType(_fixup.TypeGuids[typeRef]);
    }

    private BoxedValueRef ReadBoxedValueRef()
    {
        TypeRef typeRef = ReadTypeRef();
        long offset = _accessor.ReadInt64();

        if (typeRef.IsNull())
        {
            return new BoxedValueRef();
        }

        _accessor.StepIn(_accessor.Position + offset - 8);

        object? value = null;
        EbxExtra b = _boxedValues[(uint)(_accessor.Position - _payloadOffset)];

        if (typeRef.Type!.Name == CollectionName)
        {
            IList list = (IList)Activator.CreateInstance(typeRef.Type)!;
            value = list;
            Type elementType = typeRef.Type.GenericTypeArguments[0];
            ReadBoxedArray(b.Flags.GetTypeEnum(), b.TypeDescriptorRef, obj =>
            {
                if (TryWrapValue(elementType, ref obj))
                {
                    list.Add(obj);
                }
            });
        }
        else
        {
            if (b.Flags.GetTypeEnum() == TypeFlags.TypeEnum.Enum)
            {
                ReadBoxedField(b.Flags.GetTypeEnum(), b.TypeDescriptorRef, obj =>
                {
                    value = Enum.Parse(typeRef.Type, obj!.ToString()!);
                });
            }
            else
            {
                ReadBoxedField(b.Flags.GetTypeEnum(), b.TypeDescriptorRef, obj =>
                {
                    if (TryWrapValue(typeRef.Type, ref obj))
                    {
                        value = obj;
                    }
                });
            }
        }

        _accessor.StepOut();

        return new BoxedValueRef(value, b.Flags);
    }

    private static SdkType GetPrimitiveTypeFromEbxField(TypeFlags inFlags)
    {
        TypeFlags.TypeEnum typeEnum = inFlags.GetTypeEnum();
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        Type type = typeEnum switch
        {
            TypeFlags.TypeEnum.Void => VoidType,
            TypeFlags.TypeEnum.String => StringType,
            TypeFlags.TypeEnum.Int8 => SByteType,
            TypeFlags.TypeEnum.UInt8 => ByteType,
            TypeFlags.TypeEnum.Boolean => BoolType,
            TypeFlags.TypeEnum.UInt16 => UShortType,
            TypeFlags.TypeEnum.Int16 => ShortType,
            TypeFlags.TypeEnum.UInt32 => UIntType,
            TypeFlags.TypeEnum.Int32 => IntType,
            TypeFlags.TypeEnum.UInt64 => ULongType,
            TypeFlags.TypeEnum.Int64 => LongType,
            TypeFlags.TypeEnum.Float32 => FloatType,
            TypeFlags.TypeEnum.Float64 => DoubleType,
            TypeFlags.TypeEnum.Class => PointerType,
            TypeFlags.TypeEnum.Guid => GuidType,
            TypeFlags.TypeEnum.Sha1 => Sha1Type,
            TypeFlags.TypeEnum.CString => CStringType,
            TypeFlags.TypeEnum.ResourceRef => ResourceRefType,
            TypeFlags.TypeEnum.FileRef => FileRefType,
            TypeFlags.TypeEnum.TypeRef => TypeRefType!,
            TypeFlags.TypeEnum.BoxedValueRef => BoxedValueRefType!,
            _ => throw new NotImplementedException($"Not implemented EBX type: {typeEnum}")
        };

        return inFlags.GetCategoryEnum() == TypeFlags.CategoryEnum.Array
            ? new SdkType(typeof(ObservableCollection<>).MakeGenericType(type))
            : new SdkType(type);
    }
}