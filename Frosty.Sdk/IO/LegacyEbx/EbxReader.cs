using System.Collections;
using System.Collections.ObjectModel;
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

    internal EbxReader(DataAccessor inAccessor)
        : base(inAccessor)
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

    private void ReadType(EbxTypeDescriptor inTypeDescriptor, object? obj, long inStartOffset)
    {
        if (obj is null)
        {
            _accessor.Position += inTypeDescriptor.Size;
            _accessor.Pad(inTypeDescriptor.GetAlignment());
            return;
        }

        Type objType = obj.GetType();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < inTypeDescriptor.GetFieldCount(); i++)
        {
            EbxFieldDescriptor fieldDescriptor = _typeResolver.ResolveField(inTypeDescriptor.FieldIndex + i);
            PropertyInfo? propertyInfo = properties.FirstOrDefault(prop =>
            {
                if (string.IsNullOrEmpty(fieldDescriptor.Name))
                {
                    return prop.GetCustomAttribute<NameHashAttribute>()?.Hash == fieldDescriptor.NameHash;
                }

                return prop.GetName() == fieldDescriptor.Name;
            });

            _accessor.Position = inStartOffset + fieldDescriptor.DataOffset;

            TypeFlags.TypeEnum type = fieldDescriptor.Flags.GetTypeEnum();
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (type)
            {
                case TypeFlags.TypeEnum.Void:
                    // Read superclass first
                    ReadType(_typeResolver.ResolveType(inTypeDescriptor, fieldDescriptor.TypeDescriptorRef), obj,
                        inStartOffset);
                    break;
                case TypeFlags.TypeEnum.Array:
                    {
                        if (propertyInfo is null)
                        {
                            //FrostyLogger.Logger.LogDebug("Skipping field \"{}.{}\", because it does not exist in the type info", inTypeDescriptor.Name, fieldDescriptor.Name);
                            continue;
                        }

                        IList? list = (IList?)propertyInfo.GetValue(obj);
                        Type elementType = propertyInfo.PropertyType.GenericTypeArguments[0];
                        ReadField(inTypeDescriptor, type, fieldDescriptor.TypeDescriptorRef, value =>
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
                        //FrostyLogger.Logger.LogDebug("Skipping field \"{}.{}\", because it does not exist in the type info", inTypeDescriptor.Name, fieldDescriptor.Name);
                        continue;
                    }

                    ReadField(inTypeDescriptor, type, fieldDescriptor.TypeDescriptorRef, value =>
                    {
                        if (TryWrapValue(propertyInfo.PropertyType, ref value))
                        {
                            propertyInfo.SetValue(obj, value);
                        }
                    });
                    break;
            }
        }

        _accessor.Position = inStartOffset + inTypeDescriptor.Size;
    }

    private void ReadField(EbxTypeDescriptor? inParentTypeDescriptor, TypeFlags.TypeEnum inType,
        ushort inTypeDescriptorRef, Action<object?> inAddFunc)
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
                inAddFunc(ReadString(_accessor.ReadUInt32()));
                break;
            case TypeFlags.TypeEnum.FileRef:
                inAddFunc(ReadFileRef());
                break;
            // TODO:
            // Potential Bug: Delegates are read as a TypeRef.
            // As RiffEbx reader does,
            // a delegate property should return IType?,
            // so storing this may fails.
            case TypeFlags.TypeEnum.Delegate:
            case TypeFlags.TypeEnum.TypeRef:
                inAddFunc(ReadTypeRef());
                break;
            case TypeFlags.TypeEnum.BoxedValueRef:
                inAddFunc(ReadBoxedValueRef());
                break;
            case TypeFlags.TypeEnum.Struct:
                EbxTypeDescriptor structType = ResolveType(inParentTypeDescriptor, inTypeDescriptorRef);
                _accessor.Pad(structType.GetAlignment());
                object? obj = CreateObject(structType);
                ReadType(structType, obj, _accessor.Position);
                inAddFunc(obj);
                break;
            case TypeFlags.TypeEnum.Enum:
                inAddFunc(_accessor.ReadInt32());
                break;
            case TypeFlags.TypeEnum.Class:
                inAddFunc(ReadPointerRef());
                break;
            case TypeFlags.TypeEnum.Array:
                ReadArray(inParentTypeDescriptor, inTypeDescriptorRef, inAddFunc);
                break;
            case TypeFlags.TypeEnum.DbObject:
                throw new NotImplementedException(
                    "DbObject as EBX field type"); // Remove this line and DbObject goes to Unknown
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
            default:
                throw new InvalidDataException($"Unknown EBX field type: {inType}");
        }
    }

    private void ReadArray(EbxTypeDescriptor? inParentTypeDescriptor, ushort inTypeDescriptorRef,
        Action<object?> inAddFunc)
    {
        EbxTypeDescriptor arrayTypeDescriptor = ResolveType(inParentTypeDescriptor, inTypeDescriptorRef);

        int index = _accessor.ReadInt32();
        EbxArray array = _header.Arrays[index];

        _accessor.StepIn(_header.ArrayOffset + array.Offset);

        EbxFieldDescriptor elementFieldDescriptor = _typeResolver.ResolveField(arrayTypeDescriptor.FieldIndex);
        for (int i = 0; i < array.Count; i++)
        {
            ReadField(arrayTypeDescriptor, elementFieldDescriptor.Flags.GetTypeEnum(),
                elementFieldDescriptor.TypeDescriptorRef, inAddFunc);
        }

        _accessor.StepOut();
    }

    private EbxTypeDescriptor ResolveType(EbxTypeDescriptor? inParentTypeDescriptor, ushort inTypeDescriptorRef)
    {
        return inParentTypeDescriptor.HasValue
            ? _typeResolver.ResolveType(inParentTypeDescriptor.Value, inTypeDescriptorRef)
            : _typeResolver.ResolveType(inTypeDescriptorRef);
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

        // TODO:
        // Check is this useless code usefull
        // if (index - 1 == 1)
        // {
        // }

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

    private Type GetTypeFromEbxField(TypeFlags.TypeEnum inFlags, ushort inTypeDescriptorRef)
    {
        switch (inFlags)
        {
            case TypeFlags.TypeEnum.Struct: return GetType(_typeResolver.ResolveType(inTypeDescriptorRef));
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
                EbxTypeDescriptor arrayTypeDescriptor = _typeResolver.ResolveType(inTypeDescriptorRef);
                EbxFieldDescriptor elementFieldDescriptor = _typeResolver.ResolveField(arrayTypeDescriptor.FieldIndex);
                return typeof(ObservableCollection<>).MakeGenericType(
                    GetTypeFromEbxField(elementFieldDescriptor.Flags.GetTypeEnum(),
                        elementFieldDescriptor.TypeDescriptorRef));
            case TypeFlags.TypeEnum.Enum:
                return GetType(_typeResolver.ResolveType(inTypeDescriptorRef));
            case TypeFlags.TypeEnum.Delegate:
            case TypeFlags.TypeEnum.Void:
            case TypeFlags.TypeEnum.DbObject:
            case TypeFlags.TypeEnum.Function:
            case TypeFlags.TypeEnum.Interface:
                throw new NotImplementedException($"Not implemented in Legacy EBX reader: {inFlags}");
            default:
                throw new InvalidDataException($"Unknown EBX field type {inFlags}.");
        }
    }

    private static Type GetType(EbxTypeDescriptor inTypeDescriptor)
    {
        if (string.IsNullOrEmpty(inTypeDescriptor.Name))
        {
            return TypeLibrary.GetType(inTypeDescriptor.NameHash)?.Type ??
                   throw new InvalidDataException($"Unknown EBX type hash {inTypeDescriptor.NameHash:X8}.");
        }

        return TypeLibrary.GetType(inTypeDescriptor.Name)?.Type ??
               throw new InvalidDataException($"Unknown EBX type {inTypeDescriptor.Name}.");
    }

    private static object? CreateObject(EbxTypeDescriptor inTypeDescriptor)
    {
        return string.IsNullOrEmpty(inTypeDescriptor.Name)
            ? TypeLibrary.CreateObject(inTypeDescriptor.NameHash)
            : TypeLibrary.CreateObject(inTypeDescriptor.Name);
    }
}