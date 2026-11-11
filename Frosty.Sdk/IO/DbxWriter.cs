using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml;

using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Utils;

using static Frosty.Sdk.TypeSdk.TypeFlags;

namespace Frosty.Sdk.IO;

/// <summary>
///     Writes an <see cref="EbxPartition" /> as DBX, basically the XML version of EBX.
/// </summary>
/// <remarks><b>Encoding:</b> UTF-8 (the XML document)</remarks>
public sealed class DbxWriter : IDisposable
{
    private const string InstanceGuidName = "__InstanceGuid";
    private const string InstanceIdName = "__Id";

    private static readonly XmlWriterSettings Settings = new()
    {
        Indent = true, IndentChars = "\t", NewLineChars = "\n"
    };

    private readonly XmlWriter _xmlWriter;

    public DbxWriter(string inFilePath)
    {
        _xmlWriter = XmlWriter.Create(inFilePath, Settings);
    }

    public DbxWriter(Stream inStream)
    {
        _xmlWriter = XmlWriter.Create(inStream, Settings);
    }

    public void Dispose()
    {
        _xmlWriter.Dispose();
    }

    /// <summary>
    ///     Writes <paramref name="inPartition" />, or nothing if it has no instances.
    /// </summary>
    public void Write(EbxPartition inPartition)
    {
        ArgumentNullException.ThrowIfNull(inPartition);

        if (!inPartition.IsValid)
        {
            return;
        }

        _xmlWriter.WriteStartDocument();

        _xmlWriter.WriteStartElement("partition");
        _xmlWriter.WriteAttributeString("guid", inPartition.PartitionGuid.ToString());
        _xmlWriter.WriteAttributeString("primaryInstance", inPartition.PrimaryInstanceGuid.ToString());

        foreach (IEbxInstance instance in inPartition.InternalInstances)
        {
            WriteInstance(instance);
        }

        _xmlWriter.WriteEndElement();
    }

    /// <summary>
    ///     Gets the public instance properties of <paramref name="classType" /> and its base classes,
    ///     sorted by their <see cref="FieldIndexAttribute" /> (-1 without one).
    /// </summary>
    private static List<PropertyInfo> GetClassProperties(Type classType)
    {
        List<PropertyInfo> properties = [];
        for (Type? type = classType; type is not null; type = type.BaseType)
        {
            AddDeclaredProperties(type, properties);
        }

        // A stable sort, so properties without an index keep their order
        return
        [
            .. properties.OrderBy(static property => property.GetCustomAttribute<FieldIndexAttribute>()?.Index ?? -1)
        ];
    }

    private static void AddDeclaredProperties(Type type, List<PropertyInfo> properties)
    {
        properties.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => !property.Name.Equals(InstanceGuidName, StringComparison.Ordinal) &&
                                      !property.Name.Equals(InstanceIdName, StringComparison.Ordinal)));
    }

    private void WriteInstance(IEbxInstance instance)
    {
        Type ebxType = instance.GetType();

        _xmlWriter.WriteStartElement("instance");
        AssetClassGuid guid = instance.GetInstanceGuid();
        _xmlWriter.WriteAttributeString("guid", guid.ToString());
        _xmlWriter.WriteAttributeString("type", $"{ebxType.Namespace}.{ebxType.GetName()}");
        _xmlWriter.WriteAttributeString("exported", guid.IsExported.ToString());

        WriteProperties(GetClassProperties(ebxType), instance, static property => property.GetName());

        _xmlWriter.WriteEndElement();
    }

    private void WriteProperties(List<PropertyInfo> properties, object obj, Func<PropertyInfo, string> getName)
    {
        foreach (PropertyInfo pi in properties)
        {
            EbxFieldMetaAttribute fieldMeta = pi.GetCustomAttribute<EbxFieldMetaAttribute>()!;
            WriteField(fieldMeta.Flags.GetTypeEnum(),
                pi.GetValue(obj)!,
                pi.PropertyType,
                fieldMeta.BaseType,
                getName(pi),
                false,
                pi.GetCustomAttribute<IsTransientAttribute>() is not null,
                pi.GetCustomAttribute<IsHiddenAttribute>() is not null);
        }
    }

    #region Field Writing

    /// <summary>
    ///     Casts an EBX field to the requested type. Checks for IPrimitive types.
    /// </summary>
    private static T GetFieldValue<T>(object obj)
    {
        if (obj is not IPrimitive primitive)
        {
            return (T)obj;
        }

        return (T)primitive.ToActualType();
    }

    /// <param name="fieldType">The Frostbite type of the field.</param>
    /// <param name="obj">The value of the field.</param>
    /// <param name="objType">The .NET type of the field.</param>
    /// <param name="arrayBaseType">The element type of an array of pointers.</param>
    /// <param name="fieldName">
    ///     The name of the field, or <see langword="null" /> to write only the value into the current element.
    /// </param>
    /// <param name="isArrayItem">Writes an <c>item</c> element without a name instead of a <c>field</c>.</param>
    /// <param name="isTransient">Marks the field as transient.</param>
    /// <param name="isHidden">Marks the field as hidden.</param>
    private void WriteField(TypeEnum fieldType,
        object obj,
        Type objType,
        Type? arrayBaseType,
        string? fieldName = null,
        bool isArrayItem = false,
        bool isTransient = false,
        bool isHidden = false)
    {
        // The other types are text, see FormatValue
        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (fieldType)
        {
            case TypeEnum.Class:
                WriteFieldStart(fieldName, isArrayItem, isTransient, isHidden);
                WritePointerRef((PointerRef)obj);
                WriteFieldEnd(fieldName);
                return;
            case TypeEnum.Array:
                WriteArray(fieldName, obj, objType, arrayBaseType);
                return;
            case TypeEnum.Struct:
                WriteStruct(fieldName, obj, objType, isArrayItem);
                return;
            case TypeEnum.TypeRef:
                WriteTypeRef(fieldName!, GetFieldValue<TypeRef>(obj));
                return;
            case TypeEnum.Delegate:
                WriteDelegate(fieldName!, (IDelegate)obj);
                return;
            case TypeEnum.BoxedValueRef:
                WriteBoxedValueRef(fieldName!, GetFieldValue<BoxedValueRef>(obj));
                return;
            case TypeEnum.Void:
            case TypeEnum.DbObject:
            case TypeEnum.Function:
            case TypeEnum.Interface:
                throw new NotImplementedException($"Not implemented DBX field type: {fieldType}");
        }

        WriteFieldStart(fieldName, isArrayItem, isTransient, isHidden);
        _xmlWriter.WriteValue(FormatValue(fieldType, obj));
        WriteFieldEnd(fieldName);
    }

    /// <summary>
    ///     Formats a field that is written as text, in the format <see cref="DbxReader" /> parses.
    /// </summary>
    private static string? FormatValue(TypeEnum fieldType, object obj)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        return fieldType switch
        {
            TypeEnum.Boolean => GetFieldValue<bool>(obj).ToString(invariant),
            TypeEnum.Int8 => GetFieldValue<sbyte>(obj).ToString(invariant),
            TypeEnum.Int16 => GetFieldValue<short>(obj).ToString(invariant),
            TypeEnum.Int32 => GetFieldValue<int>(obj).ToString(invariant),
            TypeEnum.Int64 => GetFieldValue<long>(obj).ToString(invariant),
            TypeEnum.UInt8 => GetFieldValue<byte>(obj).ToString(invariant),
            TypeEnum.UInt16 => GetFieldValue<ushort>(obj).ToString(invariant),
            TypeEnum.UInt32 => GetFieldValue<uint>(obj).ToString(invariant),
            TypeEnum.UInt64 => GetFieldValue<ulong>(obj).ToString(invariant),
            TypeEnum.Float32 => GetFieldValue<float>(obj).ToString("0.0######", invariant),
            TypeEnum.Float64 => GetFieldValue<double>(obj).ToString("0.0##############", invariant),
            // TODO: Temp? Remove potential garbage from strings before writing them
            TypeEnum.CString or TypeEnum.String =>
                GetFieldValue<string?>(obj)?.Replace("\v", string.Empty, StringComparison.Ordinal),
            TypeEnum.FileRef => ((string)GetFieldValue<FileRef>(obj)).Replace("\v", string.Empty,
                StringComparison.Ordinal),
            TypeEnum.Guid => GetFieldValue<Guid>(obj).ToString("D", invariant),
            // Not Enum.GetName, which returns null for a combination of flags
            TypeEnum.Enum => ((Enum)GetFieldValue<object>(obj)).ToString(),
            TypeEnum.ResourceRef => ((ulong)GetFieldValue<ResourceRef>(obj)).ToString("X", invariant),
            TypeEnum.Sha1 => GetFieldValue<Sha1>(obj).ToString(),
            _ => throw new NotImplementedException($"Not implemented DBX field type: {fieldType}")
        };
    }

    private void WriteFieldStart(string? name, bool isArrayItem, bool isTransient, bool isHidden)
    {
        if (name is null)
        {
            return;
        }

        _xmlWriter.WriteStartElement(isArrayItem ? "item" : "field");
        if (!isArrayItem)
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

    private void WriteFieldEnd(string? name)
    {
        if (name is not null)
        {
            _xmlWriter.WriteEndElement();
        }
    }

    private void WritePointerRef(PointerRef value)
    {
        switch (value.Type)
        {
            case PointerRefType.Internal:
                {
                    _xmlWriter.WriteAttributeString("ref", value.Internal!.GetInstanceGuid().ToString());
                    break;
                }
            case PointerRefType.External:
                {
                    EbxAssetEntry? entry = AssetManager.GetEbxAssetEntry(value.External.PartitionGuid);
                    if (entry is not null)
                    {
                        _xmlWriter.WriteAttributeString("ref", $"{entry.Name}\\{value.External.InstanceGuid}");
                        _xmlWriter.WriteAttributeString("partitionGuid", entry.Guid.ToString());
                    }
                    else
                    {
                        _xmlWriter.WriteAttributeString("ref", "null");
                        _xmlWriter.WriteAttributeString("partitionGuid", "null");
                    }

                    break;
                }
            case PointerRefType.Null:
            default:
                _xmlWriter.WriteAttributeString("ref", "null");
                break;
        }
    }

    #endregion

    #region Array Writing

    private void WriteArray(string? arrayName, object arrayObj, Type arrayType, Type? arrayBaseType)
    {
        bool isRef = arrayType.GenericTypeArguments[0].Name == "PointerRef";

        Type memberType = isRef ? arrayBaseType! : arrayType.GenericTypeArguments[0];
        TypeEnum memberTypeEnum = memberType.GetCustomAttribute<EbxTypeMetaAttribute>()!.Flags.GetTypeEnum();

        string typeDisplayName = memberType.GetName();

        _xmlWriter.WriteStartElement("array");
        if (arrayName is not null)
        {
            _xmlWriter.WriteAttributeString("name", arrayName);
        }

        _xmlWriter.WriteAttributeString("type", isRef ? $"ref({typeDisplayName})" : typeDisplayName);

        foreach (object subValue in (IList)arrayObj)
        {
            WriteField(memberTypeEnum, subValue, subValue.GetType(), null, string.Empty, true);
        }

        _xmlWriter.WriteEndElement();
    }

    #endregion

    #region Struct (Complex) Writing

    private void WriteStruct(string? structName, object structObj, Type structType, bool isArrayItem = false)
    {
        _xmlWriter.WriteStartElement("complex");

        // An array item has neither a type nor a name
        if (!isArrayItem)
        {
            _xmlWriter.WriteAttributeString("type", structObj.GetType().GetName());
            if (structName is not null)
            {
                _xmlWriter.WriteAttributeString("name", structName);
            }
        }

        List<PropertyInfo> structProperties = [];
        AddDeclaredProperties(structType, structProperties);
        WriteProperties(structProperties, structObj, static property => property.Name);

        _xmlWriter.WriteEndElement();
    }

    #endregion

    #region BoxedValueRef Writing

    private void WriteBoxedValueRef(string name, BoxedValueRef boxedValue)
    {
        _xmlWriter.WriteStartElement("boxed");
        _xmlWriter.WriteAttributeString("name", name);

        if (boxedValue.Value is not null)
        {
            _xmlWriter.WriteAttributeString("typeName", boxedValue.Value.GetType().GetName());

            TypeEnum type = boxedValue.Type;
            if (boxedValue.Category == CategoryEnum.Array)
            {
                // RIFF EBX stores array in category
                type = TypeEnum.Array;
            }

            WriteField(type, boxedValue.Value, boxedValue.Value.GetType(), null);
        }

        _xmlWriter.WriteEndElement();
    }

    #endregion

    #region TypeRef Writing

    private void WriteTypeRef(string name, TypeRef typeRef)
    {
        _xmlWriter.WriteStartElement("typeref");
        _xmlWriter.WriteAttributeString("name", name);
        if (typeRef.Name is not null)
        {
            _xmlWriter.WriteAttributeString("typeName", typeRef.Name);
        }

        _xmlWriter.WriteEndElement();
    }

    #endregion

    #region Delegate Writing

    private void WriteDelegate(string name, IDelegate @delegate)
    {
        _xmlWriter.WriteStartElement("delegate");
        _xmlWriter.WriteAttributeString("name", name);
        if (@delegate.FunctionType is not null)
        {
            _xmlWriter.WriteAttributeString("typeName", @delegate.FunctionType.Name);
        }

        _xmlWriter.WriteEndElement();
    }

    #endregion
}
