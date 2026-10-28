using System.Globalization;
using System.Collections;
using System.Xml;
using System.Reflection;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Attributes;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.Utils;
using static Frosty.Sdk.TypeSdk.TypeFlags;

namespace Frosty.Sdk.IO;

public sealed class DbxWriter : IDisposable
{
    private const string InstanceGuidName = "__InstanceGuid";
    private const string InstanceIdName = "__Id";

    private readonly XmlWriterSettings _settings = new() { Indent = true, IndentChars = "\t", NewLineChars = "\n"};

    private string _filePath;
    private XmlWriter? _xmlWriter;

    public DbxWriter()
    {
        _filePath = string.Empty;
    }

    public DbxWriter(string inFilePath)
    {
        _filePath = inFilePath;
        _xmlWriter = XmlWriter.Create(_filePath, _settings);
    }

    public DbxWriter(Stream inStream)
    {
        _filePath = string.Empty;
        _xmlWriter = XmlWriter.Create(inStream, _settings);
    }

    public void Write(EbxPartition inPartition)
    {
        ArgumentNullException.ThrowIfNull(inPartition);

        if (!inPartition.IsValid)
        {
            return;
        }

        WriteAsset(inPartition);
    }

    public void Write(EbxPartition inPartition, string inFilePath)
    {
        ArgumentNullException.ThrowIfNull(inPartition);

        _xmlWriter?.Close();
        _xmlWriter = XmlWriter.Create(inFilePath, _settings);
        _filePath = inFilePath;

        if (!inPartition.IsValid)
        {
            return;
        }

        WriteAsset(inPartition);
    }

    public void Dispose()
    {
        _xmlWriter?.Dispose();
    }

    #region Partition Writing
    private void WritePartitionStart(Guid inAssetGuid, Guid inPrimaryInstanceGuid)
    {
        _xmlWriter!.WriteStartElement("partition");
        _xmlWriter.WriteAttributeString("guid", inAssetGuid.ToString());
        _xmlWriter.WriteAttributeString("primaryInstance", inPrimaryInstanceGuid.ToString());
    }

    private void WritePartitionEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    #endregion

    #region Instance Writing

    private void WriteInstanceStart(AssetClassGuid classGuid, string type, string? id = null)
    {
        _xmlWriter!.WriteStartElement("instance");
        if (id is not null)
        {
            _xmlWriter.WriteAttributeString("id", id);
        }
        _xmlWriter.WriteAttributeString("guid", classGuid.ToString());

        _xmlWriter.WriteAttributeString("type", type);
        _xmlWriter.WriteAttributeString("exported", classGuid.IsExported.ToString());
    }

    private void WriteInstanceEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    /// <summary>
    /// Writes the given EBX object to the DBX as an instance.
    /// </summary>
    private void WriteInstance(object ebxObj)
    {
        AssetClassGuid guid = ((dynamic)ebxObj).GetInstanceGuid();
        Type ebxType = ebxObj.GetType();

        WriteInstanceStart(guid,
            $"{ebxType.Namespace}.{ebxType.GetName()}");

        if (ebxType.IsClass)
        {
            WriteDbxClass(ebxType, ebxObj);
        }
        else
        {
            throw new NotImplementedException("DbxWriter: instance isn't a class?");
        }

        WriteInstanceEnd();
    }

    #endregion

    #region Field Writing

    /// <summary>
    /// Casts an EBX field to the requested type. Checks for IPrimitive types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    private static T GetFieldValue<T>(object obj)
    {
        if (obj is not IPrimitive primitive)
        {
            return (T)obj;
        }
        return (T)primitive.ToActualType();
    }

    private void WriteField(TypeEnum fieldType,
        object obj,
        Type objType,
        Type? arrayBaseType,
        string? fieldName = null,
        bool isArrayItem = false,
        bool isTransient = false,
        bool isHidden = false)
    {
        switch (fieldType)
        {
            case TypeEnum.Boolean:
                WriteFieldWithValue(fieldName, GetFieldValue<bool>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Int8:
                WriteFieldWithValue(fieldName, GetFieldValue<sbyte>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Int16:
                WriteFieldWithValue(fieldName, GetFieldValue<short>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Int32:
                WriteFieldWithValue(fieldName, GetFieldValue<int>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Int64:
                WriteFieldWithValue(fieldName, GetFieldValue<long>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.UInt8:
                WriteFieldWithValue(fieldName, GetFieldValue<byte>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.UInt16:
                WriteFieldWithValue(fieldName, GetFieldValue<ushort>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.UInt32:
                WriteFieldWithValue(fieldName, GetFieldValue<uint>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.UInt64:
                WriteFieldWithValue(fieldName, GetFieldValue<ulong>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Float32:
                WriteFieldWithValue(fieldName, GetFieldValue<float>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Float64:
                WriteFieldWithValue(fieldName, GetFieldValue<double>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.CString:
            case TypeEnum.String:
                WriteFieldWithValue(fieldName, GetFieldValue<string>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Guid:
                WriteFieldWithValue(fieldName, GetFieldValue<Guid>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Enum:
                WriteFieldWithValue(fieldName, Enum.GetName(objType, GetFieldValue<object>(obj))!, isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.ResourceRef:
                WriteFieldWithValue(fieldName, GetFieldValue<ResourceRef>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Sha1:
                WriteFieldWithValue(fieldName, GetFieldValue<Sha1>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Class:
                WriteFieldWithValue(fieldName, (PointerRef)obj, isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.Array:
                WriteArray(fieldName, obj, objType, arrayBaseType);
                break;
            case TypeEnum.Struct:
                WriteStruct(fieldName, obj, objType, isArrayItem);
                break;
            case TypeEnum.FileRef:
                WriteFieldWithValue(fieldName, GetFieldValue<FileRef>(obj), isArrayItem, isTransient, isHidden);
                break;
            case TypeEnum.TypeRef:
                WriteTypeRef(fieldName!, GetFieldValue<TypeRef>(obj));
                break;
            case TypeEnum.Delegate:
                WriteDelegate(fieldName!, (IDelegate)obj);
                break;
            case TypeEnum.BoxedValueRef:
                WriteBoxedValueRef(fieldName!, GetFieldValue<BoxedValueRef>(obj));
                break;
            case TypeEnum.Void:
            case TypeEnum.DbObject:
            case TypeEnum.Function:
            case TypeEnum.Interface:
            default:
                throw new NotImplementedException($"Not implemented DBX field type: {fieldType}");
        }
    }
    private void WriteFieldStart(string name, bool isArrayField, bool isTransient, bool isHidden)
    {
        _xmlWriter!.WriteStartElement(isArrayField ? "item" : "field");
        if (!isArrayField)
        {
            _xmlWriter.WriteAttributeString("name", name);
        }
        if (isTransient)
        {
            _xmlWriter.WriteAttributeString("transient", isTransient.ToString());
        }
        if (isHidden)
        {
            _xmlWriter.WriteAttributeString("hidden", isHidden.ToString());
        }
    }

    private void WriteFieldEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    #endregion

    #region Field Value Writing

    private void WriteFieldWithValue(string? fieldName, sbyte value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, short value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, int value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, long value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, byte value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, ushort value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, uint value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value);

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, ulong value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString(CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, ResourceRef value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(((ulong)value).ToString("X", CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, Sha1 value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString());

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, float value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString("0.0######", CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, double value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString("0.0##############", CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, bool value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString(CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, string? value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        // TODO: Temp? Remove potential garbage from strings before writing them
        _xmlWriter!.WriteValue(value?.Replace("\v", string.Empty, StringComparison.Ordinal));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, Guid value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        _xmlWriter!.WriteValue(value.ToString("D", CultureInfo.InvariantCulture));

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    private void WriteFieldWithValue(string? fieldName, PointerRef value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
    {
        if (fieldName is not null)
        {
            WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);
        }

        switch (value.Type)
        {
            case PointerRefType.Internal:
            {
                AssetClassGuid classGuid = ((dynamic)value.Internal!).GetInstanceGuid();
                _xmlWriter!.WriteAttributeString("ref", classGuid.ToString());
                break;
            }
            case PointerRefType.External:
            {
                EbxAssetEntry? entry = AssetManager.GetEbxAssetEntry(value.External.PartitionGuid);
                if (entry is not null)
                {
                    _xmlWriter!.WriteAttributeString("ref", $"{entry.Name}\\{value.External.InstanceGuid}");
                    _xmlWriter.WriteAttributeString("partitionGuid", entry.Guid.ToString());
                }
                else
                {
                    _xmlWriter!.WriteAttributeString("ref", "null");
                    _xmlWriter.WriteAttributeString("partitionGuid", "null");
                }

                break;
            }
            case PointerRefType.Null:
            default:
                _xmlWriter!.WriteAttributeString("ref", "null");
                break;
        }

        if (fieldName is not null)
        {
            WriteFieldEnd();
        }
    }

    #endregion

    #region Array Writing
    private void WriteArray(string? arrayName, object arrayObj, Type arrayType, Type? arrayBaseType)
    {
        bool isRef = arrayType.GenericTypeArguments[0].Name == "PointerRef";

        Type memberType = isRef ? arrayBaseType! : arrayType.GenericTypeArguments[0];
        EbxTypeMetaAttribute memberMeta = memberType.GetCustomAttribute<EbxTypeMetaAttribute>()!;

        string typeDisplayName = memberType.GetName();

        WriteArrayStart(arrayName, isRef ? $"ref({typeDisplayName})" : typeDisplayName);

        IList list = (IList)arrayObj;

        int count = list.Count;

        for (int i = 0; i < count; i++)
        {
            object subValue = list[i]!;
            WriteField(memberMeta.Flags.GetTypeEnum(), subValue, subValue.GetType(), null, string.Empty, true);
        }

        WriteArrayEnd();
    }

    private void WriteArrayStart(string? name, string? memberType = null)
    {
        _xmlWriter!.WriteStartElement("array");
        if (name is not null)
        {
            _xmlWriter.WriteAttributeString("name", name);
        }

        if (memberType is not null)
        {
            _xmlWriter.WriteAttributeString("type", memberType);
        }
    }

    private void WriteArrayEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    #endregion

    #region Struct (Complex) Writing

    private void WriteStruct(string? structName,
        object structObj,
        Type structType,
        bool isArrayItem = false)
    {
        WriteStructStart(structName, isArrayItem ? null : structObj.GetType().GetName());

        List<PropertyInfo> structProperties = [];
        GetAllProperties(structType, ref structProperties);

        foreach (PropertyInfo pi in structProperties)
        {
            EbxFieldMetaAttribute? fieldMeta = pi.GetCustomAttribute<EbxFieldMetaAttribute>();
            WriteField(fieldMeta!.Flags.GetTypeEnum(),
                pi.GetValue(structObj)!,
                pi.PropertyType,
                fieldMeta.BaseType,
                pi.Name,
                false,
                pi.GetCustomAttribute<IsTransientAttribute>() is not null,
                pi.GetCustomAttribute<IsHiddenAttribute>() is not null);
        }

        WriteStructEnd();
    }

    private void WriteStructStart(string? name = null, string? type = null)
    {
        _xmlWriter!.WriteStartElement("complex");
        if (type is null)
        {
            return;
        }

        _xmlWriter.WriteAttributeString("type", type);
        if (name is not null)
        {
            _xmlWriter.WriteAttributeString("name", name);
        }
    }

    private void WriteStructEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    #endregion

    #region BoxedValueRef Writing

    private void WriteBoxedValueRefStart(string name, string? valueType)
    {
        _xmlWriter!.WriteStartElement("boxed");
        _xmlWriter.WriteAttributeString("name", name);
        if (valueType is not null)
        {
            _xmlWriter.WriteAttributeString("typeName", valueType);
        }
    }

    private void WriteBoxedValueRefEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    private void WriteBoxedValueRef(string name, BoxedValueRef boxedValue)
    {
        WriteBoxedValueRefStart(name, boxedValue.Value?.GetType().GetName());
        if (boxedValue.Value is not null)
        {
            TypeEnum type = boxedValue.Type;
            if (boxedValue.Category == CategoryEnum.Array)
            {
                // RIFF EBX stores array in category
                type = TypeEnum.Array;
            }
            WriteField(type, boxedValue.Value, boxedValue.Value.GetType(), null);
        }
        WriteBoxedValueRefEnd();
    }

    #endregion

    #region TypeRef Writing

    private void WriteTypeRefStart(string name)
    {
        _xmlWriter!.WriteStartElement("typeref");
        _xmlWriter.WriteAttributeString("name", name);
    }

    private void WriteTypeRefEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    private void WriteTypeRef(string name, TypeRef typeRef)
    {
        WriteTypeRefStart(name);
        if (typeRef.Name is not null)
        {
            _xmlWriter!.WriteAttributeString("typeName", typeRef.Name);
        }

        WriteTypeRefEnd();
    }

    #endregion

    #region Delegate Writing

    private void WriteDelegateStart(string name)
    {
        _xmlWriter!.WriteStartElement("delegate");
        _xmlWriter.WriteAttributeString("name", name);
    }

    private void WriteDelegateEnd()
    {
        _xmlWriter!.WriteEndElement();
    }

    private void WriteDelegate(string name, IDelegate @delegate)
    {
        WriteDelegateStart(name);
        if (@delegate.FunctionType is not null)
        {
            _xmlWriter!.WriteAttributeString("typeName", @delegate.FunctionType.Name);
        }

        WriteDelegateEnd();
    }

    #endregion

    private void WriteAsset(EbxPartition inPartition)
    {
        _xmlWriter!.WriteStartDocument();

        WritePartitionStart(inPartition.PartitionGuid, inPartition.PrimaryInstanceGuid);

        foreach (IEbxInstance ebxObj in inPartition.InternalInstances)
        {
            WriteInstance(ebxObj);
        }

        WritePartitionEnd();
    }

    /// <summary>
    /// Writes all properties in the class and its base classes as fields.
    /// </summary>
    /// <param name="classType"></param>
    /// <param name="classObj"></param>
    private void WriteDbxClass(Type classType, object classObj)
    {
        List<PropertyInfo> classProperties = [];
        GetAllProperties(classType, ref classProperties, true, true);

        if (classProperties.Count == 0)
        {
            return;
        }

        foreach (PropertyInfo pi in classProperties)
        {
            EbxFieldMetaAttribute? fieldMeta = pi.GetCustomAttribute<EbxFieldMetaAttribute>();
            WriteField(fieldMeta!.Flags.GetTypeEnum(),
                pi.GetValue(classObj)!,
                pi.PropertyType,
                fieldMeta.BaseType,
                pi.GetName(),
                false,
                pi.GetCustomAttribute<IsTransientAttribute>() is not null,
                pi.GetCustomAttribute<IsHiddenAttribute>() is not null);
        }
    }

    /// <summary>
    /// Gets all properties of the given type.
    /// </summary>
    /// <param name="classType">Type to get properties from.</param>
    /// <param name="properties">List of PropertyInfo to add the properties to.</param>
    /// <param name="checkBaseTypes">If true, recursively gathers all properties from this type and any of it's base types.</param>
    /// <param name="shouldSort">If true, properties will be sorted by the index stored in their FieldIndexAttribute. If the property doesn't have this attribute, it's index will default to -1.</param>
    private static void GetAllProperties(Type classType, ref List<PropertyInfo> properties, bool checkBaseTypes = false, bool shouldSort = false)
    {
        PropertyInfo[] currentTypeProps = classType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        properties.AddRange(currentTypeProps.Where(static pi =>
            !pi.Name.Equals(InstanceGuidName, StringComparison.Ordinal) &&
            !pi.Name.Equals(InstanceIdName, StringComparison.Ordinal)));

        if (checkBaseTypes)
        {
            Type? baseType = classType.BaseType;
            if (baseType is not null)
            {
                GetAllProperties(baseType, ref properties, checkBaseTypes, shouldSort);
            }
        }

        if (shouldSort)
        {
            properties.Sort((p1, p2) =>
            {
                int index1 = p1.GetCustomAttribute<FieldIndexAttribute>()?.Index ?? -1;
                int index2 = p2.GetCustomAttribute<FieldIndexAttribute>()?.Index ?? -1;

                return index1.CompareTo(index2);
            });
        }
    }
}
