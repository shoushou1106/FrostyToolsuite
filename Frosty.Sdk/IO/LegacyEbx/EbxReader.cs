using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;

using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Ebx;
using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.IO.LegacyEbx;

public sealed class EbxReader : BaseEbxReader
{
    private readonly EbxHeader _header;
    private readonly EbxTypeResolver _typeResolver;

    internal EbxReader(DataAccessor accessor)
        : base(accessor)
    {
        _header = EbxHeader.ReadHeader(_accessor);
        _typeResolver = new EbxTypeResolver(_header.TypeDescriptors, _header.FieldDescriptors);
    }

    public override Guid GetPartitionGuid()
    {
        return _header.PartitionGuid;
    }

    public override string GetRootType()
    {
        EbxTypeDescriptor type = _typeResolver.ResolveType(_header.Instances[0].TypeDescriptorRef);
        // Just use the name of the type if it's not stripped
        return _header.TypeNameTableLength > 0 ? type.Name : GetType(type).GetName();
    }

    public override HashSet<Guid> GetDependencies()
    {
        return _header.Dependencies;
    }

    protected override void InternalReadObjects()
    {
        _accessor.Position = _header.StringsOffset + _header.StringTableLength;

        foreach (EbxInstance instance in _header.Instances)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(instance.TypeDescriptorRef);
            for (int i = 0; i < instance.Count; i++)
            {
                _instances.Add((IEbxInstance?)CreateObject(typeDescriptor));
                _refCounts.Add(0);
            }
        }

        int objectIndex = 0;
        foreach (EbxInstance instance in _header.Instances)
        {
            EbxTypeDescriptor typeDescriptor = _typeResolver.ResolveType(instance.TypeDescriptorRef);
            for (int j = 0; j < instance.Count; j++)
            {
                _accessor.Pad(typeDescriptor.GetAlignment());

                Guid instanceGuid = instance.IsExported ? _accessor.ReadGuid() : Guid.Empty;

                if (typeDescriptor.GetAlignment() != 0x04)
                {
                    _accessor.Position += 8;
                }

                IEbxInstance? obj = _instances[objectIndex];
                obj?.SetInstanceGuid(new AssetClassGuid(instanceGuid, objectIndex));
                objectIndex++; // Do not put this i++ inside ?.
                ReadType(typeDescriptor, obj, _accessor.Position - 8);
            }
        }
    }

    private void ReadType(EbxTypeDescriptor typeDescriptor, object? obj, long startOffset)
    {
        if (obj is null)
        {
            _accessor.Position += typeDescriptor.Size;
            _accessor.Pad(typeDescriptor.GetAlignment());
            return;
        }

        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < typeDescriptor.GetFieldCount(); i++)
        {
            EbxFieldDescriptor fieldDescriptor = _typeResolver.ResolveField(typeDescriptor.FieldIndex + i);
            PropertyInfo? propertyInfo = properties.FirstOrDefault(prop =>
            {
                if (string.IsNullOrEmpty(fieldDescriptor.Name))
                {
                    return prop.GetCustomAttribute<NameHashAttribute>()?.Hash == fieldDescriptor.NameHash;
                }

                return prop.GetName() == fieldDescriptor.Name;
            });

            _accessor.Position = startOffset + fieldDescriptor.DataOffset;

            TypeFlags.TypeEnum type = fieldDescriptor.Flags.GetTypeEnum();
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (type)
            {
                case TypeFlags.TypeEnum.Void:
                    // Read superclass first
                    ReadType(_typeResolver.ResolveType(typeDescriptor, fieldDescriptor.TypeDescriptorRef), obj,
                        startOffset);
                    break;
                case TypeFlags.TypeEnum.Array:
                    {
                        if (propertyInfo is null)
                        {
                            // The field does not exist in the type info
                            continue;
                        }

                        IList? list = (IList?)propertyInfo.GetValue(obj);
                        Type elementType = propertyInfo.PropertyType.GenericTypeArguments[0];
                        ReadField(typeDescriptor, type, fieldDescriptor.TypeDescriptorRef, value =>
                        {
                            if (TryWrapValue(elementType, ref value))
                            {
                                list?.Add(value);
                            }
                        });
                        break;
                    }
                default:
                    if (propertyInfo is null)
                    {
                        // The field does not exist in the type info
                        continue;
                    }

                    ReadField(typeDescriptor, type, fieldDescriptor.TypeDescriptorRef, value =>
                    {
                        if (TryWrapValue(propertyInfo.PropertyType, ref value))
                        {
                            propertyInfo.SetValue(obj, value);
                        }
                    });
                    break;
            }
        }

        _accessor.Position = startOffset + typeDescriptor.Size;
    }

    private void ReadField(EbxTypeDescriptor? parentTypeDescriptor, TypeFlags.TypeEnum type,
        ushort typeDescriptorRef, Action<object?> addFunc)
    {
        switch (type)
        {
            case TypeFlags.TypeEnum.Boolean:
                addFunc(_accessor.ReadBoolean());
                break;
            case TypeFlags.TypeEnum.Int8:
                addFunc((sbyte)_accessor.ReadByte());
                break;
            case TypeFlags.TypeEnum.UInt8:
                addFunc(_accessor.ReadByte());
                break;
            case TypeFlags.TypeEnum.Int16:
                addFunc(_accessor.ReadInt16());
                break;
            case TypeFlags.TypeEnum.UInt16:
                addFunc(_accessor.ReadUInt16());
                break;
            case TypeFlags.TypeEnum.Int32:
                addFunc(_accessor.ReadInt32());
                break;
            case TypeFlags.TypeEnum.UInt32:
                addFunc(_accessor.ReadUInt32());
                break;
            case TypeFlags.TypeEnum.Int64:
                addFunc(_accessor.ReadInt64());
                break;
            case TypeFlags.TypeEnum.UInt64:
                addFunc(_accessor.ReadUInt64());
                break;
            case TypeFlags.TypeEnum.Float32:
                addFunc(_accessor.ReadSingle());
                break;
            case TypeFlags.TypeEnum.Float64:
                addFunc(_accessor.ReadDouble());
                break;
            case TypeFlags.TypeEnum.Guid:
                addFunc(_accessor.ReadGuid());
                break;
            case TypeFlags.TypeEnum.ResourceRef:
                addFunc(ReadResourceRef());
                break;
            case TypeFlags.TypeEnum.Sha1:
                addFunc(_accessor.ReadSha1());
                break;
            case TypeFlags.TypeEnum.String:
                addFunc(_accessor.ReadFixedSizedString(32));
                break;
            case TypeFlags.TypeEnum.CString:
                addFunc(ReadString(_accessor.ReadUInt32()));
                break;
            case TypeFlags.TypeEnum.FileRef:
                addFunc(ReadFileRef());
                break;
            case TypeFlags.TypeEnum.Delegate:
                // Stored like a TypeRef, but a delegate takes the function type as IType
                addFunc(ReadTypeRef().InternalType);
                break;
            case TypeFlags.TypeEnum.TypeRef:
                addFunc(ReadTypeRef());
                break;
            case TypeFlags.TypeEnum.BoxedValueRef:
                addFunc(ReadBoxedValueRef());
                break;
            case TypeFlags.TypeEnum.Struct:
                EbxTypeDescriptor structType = ResolveType(parentTypeDescriptor, typeDescriptorRef);
                _accessor.Pad(structType.GetAlignment());
                object? obj = CreateObject(structType);
                ReadType(structType, obj, _accessor.Position);
                addFunc(obj);
                break;
            case TypeFlags.TypeEnum.Enum:
                addFunc(_accessor.ReadInt32());
                break;
            case TypeFlags.TypeEnum.Class:
                addFunc(ReadPointerRef());
                break;
            case TypeFlags.TypeEnum.Array:
                ReadArray(parentTypeDescriptor, typeDescriptorRef, addFunc);
                break;
            case TypeFlags.TypeEnum.DbObject:
                throw new NotImplementedException("DbObject as EBX field type");
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
            default:
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"Unknown EBX field type: {type}"));
        }
    }

    private void ReadArray(EbxTypeDescriptor? parentTypeDescriptor, ushort typeDescriptorRef,
        Action<object?> addFunc)
    {
        EbxTypeDescriptor arrayTypeDescriptor = ResolveType(parentTypeDescriptor, typeDescriptorRef);

        int index = _accessor.ReadInt32();
        EbxArray array = _header.Arrays[index];

        _accessor.StepIn(_header.ArrayOffset + array.Offset);

        EbxFieldDescriptor elementFieldDescriptor = _typeResolver.ResolveField(arrayTypeDescriptor.FieldIndex);
        for (int i = 0; i < array.Count; i++)
        {
            ReadField(arrayTypeDescriptor, elementFieldDescriptor.Flags.GetTypeEnum(),
                elementFieldDescriptor.TypeDescriptorRef, addFunc);
        }

        _accessor.StepOut();
    }

    private EbxTypeDescriptor ResolveType(EbxTypeDescriptor? parentTypeDescriptor, ushort typeDescriptorRef)
    {
        return parentTypeDescriptor.HasValue
            ? _typeResolver.ResolveType(parentTypeDescriptor.Value, typeDescriptorRef)
            : _typeResolver.ResolveType(typeDescriptorRef);
    }

    /// <remarks><b>Encoding:</b> UTF-8</remarks>
    private string ReadString(uint offset)
    {
        if (offset == 0xFFFFFFFF)
        {
            return string.Empty;
        }

        long pos = _accessor.Position;
        _accessor.Position = _header.StringsOffset + offset;

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
        uint index = (uint)_accessor.ReadUInt64();

        return new FileRef(ReadString(index));
    }

    private PointerRef ReadPointerRef()
    {
        uint index = _accessor.ReadUInt32();

        if (index >> 0x1F == 1)
        {
            EbxImportReference import = _header.Imports[(int)(index & 0x7FFFFFFF)];

            return new PointerRef(import);
        }

        if (index == 0)
        {
            return new PointerRef();
        }

        IEbxInstance? obj = _instances[(int)(index - 1)];
        if (obj is null)
        {
            return new PointerRef();
        }

        _refCounts[(int)(index - 1)]++;
        return new PointerRef(obj);
    }

    private TypeRef ReadTypeRef()
    {
        string str = ReadString(_accessor.ReadUInt32());

        if (string.IsNullOrEmpty(str))
        {
            return new TypeRef();
        }

        if (!Guid.TryParse(str, out Guid guid))
        {
            return new TypeRef(str);
        }

        return guid == Guid.Empty ? new TypeRef() : new TypeRef(guid);
    }

    private BoxedValueRef ReadBoxedValueRef()
    {
        int index = _accessor.ReadInt32();
        _accessor.Position += 12;

        if (index == -1)
        {
            return new BoxedValueRef();
        }

        EbxBoxedValue boxedValue = _header.BoxedValues[index];

        long pos = _accessor.Position;
        _accessor.Position = _header.BoxedValueOffset + boxedValue.Offset;

        object? value = null;
        TypeFlags.TypeEnum type = (TypeFlags.TypeEnum)boxedValue.Type;
        Type fieldType = GetTypeFromEbxField(type, boxedValue.TypeDescriptorRef);
        if (type == TypeFlags.TypeEnum.Enum)
        {
            ReadField(null, type, boxedValue.TypeDescriptorRef, obj =>
            {
                value = Enum.Parse(fieldType, obj!.ToString()!);
            });
        }
        else
        {
            IList? list = type == TypeFlags.TypeEnum.Array ? (IList)Activator.CreateInstance(fieldType)! : null;
            value = list;
            Type elementType = list is not null ? fieldType.GenericTypeArguments[0] : fieldType;

            ReadField(null, type, boxedValue.TypeDescriptorRef, obj =>
            {
                if (!TryWrapValue(elementType, ref obj))
                {
                    return;
                }

                if (list is not null)
                {
                    list.Add(obj);
                }
                else
                {
                    value = obj;
                }
            });
        }

        _accessor.Position = pos;

        return new BoxedValueRef(value, new TypeFlags((TypeFlags.TypeEnum)boxedValue.Type));
    }

    private Type GetTypeFromEbxField(TypeFlags.TypeEnum flags, ushort typeDescriptorRef)
    {
        switch (flags)
        {
            case TypeFlags.TypeEnum.Struct: return GetType(_typeResolver.ResolveType(typeDescriptorRef));
            case TypeFlags.TypeEnum.String: return StringType;
            case TypeFlags.TypeEnum.Int8: return SByteType;
            case TypeFlags.TypeEnum.UInt8: return ByteType;
            case TypeFlags.TypeEnum.Boolean: return BoolType;
            case TypeFlags.TypeEnum.UInt16: return UShortType;
            case TypeFlags.TypeEnum.Int16: return ShortType;
            case TypeFlags.TypeEnum.UInt32: return UIntType;
            case TypeFlags.TypeEnum.Int32: return IntType;
            case TypeFlags.TypeEnum.UInt64: return ULongType;
            case TypeFlags.TypeEnum.Int64: return LongType;
            case TypeFlags.TypeEnum.Float32: return FloatType;
            case TypeFlags.TypeEnum.Float64: return DoubleType;
            case TypeFlags.TypeEnum.Class: return PointerType;
            case TypeFlags.TypeEnum.Guid: return GuidType;
            case TypeFlags.TypeEnum.Sha1: return Sha1Type;
            case TypeFlags.TypeEnum.CString: return CStringType;
            case TypeFlags.TypeEnum.ResourceRef: return ResourceRefType;
            case TypeFlags.TypeEnum.FileRef: return FileRefType;
            case TypeFlags.TypeEnum.TypeRef: return TypeRefType!;
            case TypeFlags.TypeEnum.BoxedValueRef: return BoxedValueRefType!;
            case TypeFlags.TypeEnum.Array:
                EbxTypeDescriptor arrayTypeDescriptor = _typeResolver.ResolveType(typeDescriptorRef);
                EbxFieldDescriptor elementFieldDescriptor = _typeResolver.ResolveField(arrayTypeDescriptor.FieldIndex);
                return typeof(ObservableCollection<>).MakeGenericType(
                    GetTypeFromEbxField(elementFieldDescriptor.Flags.GetTypeEnum(),
                        elementFieldDescriptor.TypeDescriptorRef));
            case TypeFlags.TypeEnum.Enum:
                return GetType(_typeResolver.ResolveType(typeDescriptorRef));
            case TypeFlags.TypeEnum.Delegate:
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.DbObject:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
                throw new NotImplementedException(
                    string.Create(CultureInfo.InvariantCulture, $"Not implemented in Legacy EBX reader: {flags}"));
            default:
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"Unknown EBX field type {flags}."));
        }
    }

    private static Type GetType(EbxTypeDescriptor typeDescriptor)
    {
        if (string.IsNullOrEmpty(typeDescriptor.Name))
        {
            return TypeLibrary.GetType(typeDescriptor.NameHash)?.Type ??
                   throw new InvalidDataException(
                       string.Create(CultureInfo.InvariantCulture,
                           $"Unknown EBX type hash {typeDescriptor.NameHash:X8}."));
        }

        return TypeLibrary.GetType(typeDescriptor.Name)?.Type ??
               throw new InvalidDataException(
                   string.Create(CultureInfo.InvariantCulture, $"Unknown EBX type {typeDescriptor.Name}."));
    }

    private static object? CreateObject(EbxTypeDescriptor typeDescriptor)
    {
        return string.IsNullOrEmpty(typeDescriptor.Name)
            ? TypeLibrary.CreateObject(typeDescriptor.NameHash)
            : TypeLibrary.CreateObject(typeDescriptor.Name);
    }
}