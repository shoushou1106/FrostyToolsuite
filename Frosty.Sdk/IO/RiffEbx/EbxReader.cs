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
    private long _payloadOffset;
    private EbxFixup _fixup;
    private readonly EbxTypeResolver _typeResolver;

    private readonly Dictionary<uint, EbxExtra> _boxedValues = new();

    private const string CollectionName = "ObservableCollection`1";

    public EbxReader(DataStream inStream)
        : base(inStream)
    {
        RiffStream riffStream = new(_stream);
        riffStream.ReadHeader(out FourCc fourCc, out uint size);

        if (fourCc != "EBX\0" && fourCc != "EBXS")
        {
            throw new InvalidDataException("Not a valid ebx chunk");
        }

        // Read EBXD chunk
        riffStream.ReadNextChunk(ref size, ProcessChunk);
        // Read EFIX chunk
        riffStream.ReadNextChunk(ref size, ProcessChunk);

        _typeResolver = new EbxTypeResolver(_fixup.TypeGuids, _fixup.TypeSignatures);

        // Read EBXX chunk
        riffStream.ReadNextChunk(ref size, ProcessChunk);

        Debug.Assert(riffStream.Eof);
    }

    public override Guid GetPartitionGuid() => _fixup.PartitionGuid;

    public override string GetRootType()
    {
        _stream.Position = _payloadOffset + _fixup.InstanceOffsets[0];
        return TypeLibrary.GetType(_fixup.TypeGuids[_stream.ReadUInt16()])?.Name ?? throw new InvalidDataException("Unknown EBX root type.");
    }

    public override HashSet<Guid> GetDependencies() => _fixup.Dependencies;

    private void ProcessChunk(DataStream inStream, FourCc inFourCc, uint inSize)
    {
        switch ((string)inFourCc)
        {
            case "EBXD":
                ReadDataChunk(inStream);
                break;
            case "EFIX":
                ReadFixupChunk(inStream);
                break;
            case "EBXX":
                ReadXChunk(inStream);
                break;
        }
    }

    protected override void InternalReadObjects()
    {
        int[] typeRefs = new int[_fixup.InstanceOffsets.Count];
        for (int i = 0; i < typeRefs.Length; i++)
        {
            _stream.Position = _payloadOffset + _fixup.InstanceOffsets[i];
            typeRefs[i] = _stream.ReadUInt16();

            _instances.Add((IEbxInstance?)TypeLibrary.CreateObject(_typeResolver.ResolveType(typeRefs[i]).NameHash));
            _refCounts.Add(0);
        }

        _stream.Position = _payloadOffset;

        for (int i = 0; i < _fixup.InstanceOffsets.Count; i++)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(typeRefs[i]);

            _stream.Pad(typeDescriptor.Alignment);

            Guid instanceGuid = i < _fixup.ExportedInstanceCount ? _stream.ReadGuid() : Guid.Empty;

            Debug.Assert(_stream.Position - _payloadOffset == _fixup.InstanceOffsets[i]);

            IEbxInstance? obj = _instances[i];
            obj?.SetInstanceGuid(new AssetClassGuid(instanceGuid, i));
            ReadType(typeDescriptor, obj, _stream.Position);
        }
    }

    private void ReadFixupChunk(DataStream inStream)
    {
        _fixup = EbxFixup.ReadFixup(inStream);
    }

    private void ReadDataChunk(DataStream inStream)
    {
        inStream.Pad(16);
        _payloadOffset = inStream.Position;
    }

    private void ReadXChunk(DataStream inStream)
    {
        int arrayCount = inStream.ReadInt32();
        int boxedValueCount = inStream.ReadInt32();
        _boxedValues.EnsureCapacity(boxedValueCount);

        inStream.Position += arrayCount * (sizeof(uint) + sizeof(int) + sizeof(uint) + sizeof(ushort) + sizeof(ushort));

        for (int i = 0; i < boxedValueCount; i++)
        {
            EbxExtra b = new()
            {
                Offset = inStream.ReadUInt32(),
                Count = inStream.ReadInt32(),
                Hash = inStream.ReadUInt32(),
                Flags = inStream.ReadUInt16(),
                TypeDescriptorRef = inStream.ReadUInt16()
            };

            _boxedValues.Add(b.Offset, b);
        }
    }

    private void ReadType(EbxTypeDescriptor inTypeDescriptor, object? obj, long inStartOffset)
    {
        if (obj is null)
        {
            _stream.Position = inStartOffset + inTypeDescriptor.Size;
            _stream.Pad(inTypeDescriptor.Alignment);
            return;
        }

        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties();

        for (int i = 0; i < inTypeDescriptor.FieldCount; i++)
        {
            EbxFieldDescriptor fieldDescriptor = EbxTypeResolver.ResolveField(inTypeDescriptor.FieldIndex + i);
            PropertyInfo? propertyInfo = properties.FirstOrDefault(prop =>
                prop.GetCustomAttribute<NameHashAttribute>()?.Hash == fieldDescriptor.NameHash);

            _stream.Position = inStartOffset + fieldDescriptor.DataOffset;

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
                    ReadType(EbxTypeResolver.ResolveTypeFromField(fieldDescriptor.TypeDescriptorRef), obj, inStartOffset);
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
        _stream.Position = inStartOffset + inTypeDescriptor.Size;
    }

    private void SharedReadField(TypeFlags.TypeEnum inType, Action<object?> inAddFunc)
    {
        switch (inType)
        {
            case TypeFlags.TypeEnum.Boolean:
                inAddFunc(_stream.ReadBoolean());
                break;
            case TypeFlags.TypeEnum.Int8:
                inAddFunc((sbyte)_stream.ReadByte());
                break;
            case TypeFlags.TypeEnum.UInt8:
                inAddFunc(_stream.ReadByte());
                break;
            case TypeFlags.TypeEnum.Int16:
                inAddFunc(_stream.ReadInt16());
                break;
            case TypeFlags.TypeEnum.UInt16:
                inAddFunc(_stream.ReadUInt16());
                break;
            case TypeFlags.TypeEnum.Int32:
                inAddFunc(_stream.ReadInt32());
                break;
            case TypeFlags.TypeEnum.UInt32:
                inAddFunc(_stream.ReadUInt32());
                break;
            case TypeFlags.TypeEnum.Int64:
                inAddFunc(_stream.ReadInt64());
                break;
            case TypeFlags.TypeEnum.UInt64:
                inAddFunc(_stream.ReadUInt64());
                break;
            case TypeFlags.TypeEnum.Float32:
                inAddFunc(_stream.ReadSingle());
                break;
            case TypeFlags.TypeEnum.Float64:
                inAddFunc(_stream.ReadDouble());
                break;
            case TypeFlags.TypeEnum.Guid:
                inAddFunc(_stream.ReadGuid());
                break;
            case TypeFlags.TypeEnum.ResourceRef:
                inAddFunc(ReadResourceRef());
                break;
            case TypeFlags.TypeEnum.Sha1:
                inAddFunc(_stream.ReadSha1());
                break;
            case TypeFlags.TypeEnum.String:
                inAddFunc(_stream.ReadFixedSizedString(32));
                break;
            case TypeFlags.TypeEnum.CString:
                inAddFunc(ReadString(_stream.ReadInt64()));
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
                inAddFunc(_stream.ReadInt32());
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
        _stream.Pad(structType.Alignment);
        object? obj = TypeLibrary.CreateObject(structType.NameHash);
        ReadType(structType, obj, _stream.Position);
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
        _stream.Pad(structType.Alignment);
        object? obj = TypeLibrary.CreateObject(structType.NameHash);
        ReadType(structType, obj, _stream.Position);
        inAddFunc(obj);
    }

    private void ReadArray(EbxFieldDescriptor inFieldDescriptor,
        Action<object?> inAddFunc)
    {
        int offset = _stream.ReadInt32();

        long arrayPos = _stream.Position;

        _stream.Position += offset - 8;

        int count = _stream.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            ReadField(inFieldDescriptor, inAddFunc);
        }

        _stream.Position = arrayPos;
    }

    private void ReadBoxedArray(TypeFlags.TypeEnum inType, int inTypeDescriptorRef,
        Action<object?> inAddFunc)
    {
        int offset = _stream.ReadInt32();

        long arrayPos = _stream.Position;

        _stream.Position += offset - 8;

        int count = _stream.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            ReadBoxedField(inType, inTypeDescriptorRef, inAddFunc);
        }

        _stream.Position = arrayPos;
    }

    /// <remarks><b>Encoding:</b> UTF-8</remarks>
    private string ReadString(long offset)
    {
        if (offset == -1)
        {
            return string.Empty;
        }

        long pos = _stream.Position;
        _stream.Position += offset - 8;

        string retStr = _stream.ReadNullTerminatedString();
        _stream.Position = pos;

        return retStr;
    }

    private ResourceRef ReadResourceRef() => new(_stream.ReadUInt64());

    private FileRef ReadFileRef()
    {
        return new FileRef(ReadString(_stream.ReadInt64()));
    }

    private PointerRef ReadPointerRef()
    {
        int index = (int)_stream.ReadInt64();

        if (index == 0)
        {
            return new PointerRef();
        }

        if ((index & 1) == 1)
        {
            return new PointerRef(_fixup.Imports[index >> 1]);
        }

        long instanceOffset = _stream.Position - 8 + index - _payloadOffset;
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
        uint packed = _stream.ReadUInt32();
        _stream.Position += 4; // 0 (index in packed) or -1 (primitive)

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
        uint packed = _stream.ReadUInt32();
        _stream.Position += 4;

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
        long offset = _stream.ReadInt64();

        if (typeRef.IsNull())
        {
            return new BoxedValueRef();
        }

        _stream.StepIn(_stream.Position + offset - 8);

        object? value = null;
        EbxExtra b = _boxedValues[(uint)(_stream.Position - _payloadOffset)];

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

        _stream.StepOut();

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