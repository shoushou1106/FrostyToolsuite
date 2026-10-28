using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Ebx;
using static Frosty.Sdk.TypeSdk.TypeFlags;
using System.Globalization;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Collections.ObjectModel;

using Microsoft.Extensions.Logging;

namespace Frosty.Sdk.IO;

public sealed class DbxReader
{
    private static readonly Type PointerType = typeof(PointerRef);
    private static readonly Type CollectionType = typeof(ObservableCollection<>);
    private static readonly Type? BoxedValueRefType = TypeLibrary.GetType("BoxedValueRef")?.Type;
    private static readonly Type? TypeRefType = TypeLibrary.GetType("TypeRef")?.Type;

    // Only write out properties with these flags
    private const BindingFlags PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;

    // The loaded dbx file
    private readonly XmlDocument _xml = new();
    // Key - Instance guid, value - Instance and its XML representation
    private readonly Dictionary<Guid, (IEbxInstance ebxInstance, XmlNode dbxInstance)> _guidToObjAndXmlNode = new();
    // Used to keep track of number of refs pointing to an instance
    private readonly Dictionary<Guid, int> _guidToRefCount = new();

    private EbxPartition? _ebx;
    // (Never used) private Guid m_primaryInstGuid;
    
    // Incremented when an internal id is requested
    private int _internalId = -1;
    private readonly string _filepath = string.Empty;

    public DbxReader(string filepath)
    {
        _filepath = filepath;
        _xml.Load(filepath);
    }

    public DbxReader(Stream inStream)
    {
        _xml.Load(inStream);
    }

    public EbxPartition ReadAsset()
    {
        _ebx = new EbxPartition();
#if DEBUG
        Stopwatch w = new();
        w.Start();
#endif
        XmlNode? rootNode = _xml.DocumentElement;

        Debug.Assert(rootNode is { Name: "partition" }, "Invalid DBX (Root element is not a partition)");

        ReadPartition(rootNode);
#if DEBUG
        w.Stop();
        if (FrostyLogger.Logger.IsEnabled(LogLevel.Debug))
        {
            FrostyLogger.Logger.LogDebug("DBX {Filepath} read in {ElapsedMilliseconds} ms", _filepath, w.ElapsedMilliseconds);
        }
#endif
        return _ebx!;
    }

    private static void SetProperty(object obj, Type objType, string propName, object propValue)
    {
        PropertyInfo? property = objType.GetProperty(propName, PropertyBindingFlags);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(obj, propValue);
    }

    private static void SetPropertyFromStringValue(object obj, Type objType, string propName, string propValue)
    {
        PropertyInfo? property = objType.GetProperty(propName, PropertyBindingFlags);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        object value = GetValueFromString(property.PropertyType, propValue,
            property.GetCustomAttribute<EbxFieldMetaAttribute>()?.Flags.GetTypeEnum());
        property.SetValue(obj, value);
    }

    private static object GetValueFromString(Type propType, string propValue, TypeEnum? frostbiteType = null)
    {
        if (propType.IsPrimitive)
        {
            return Convert.ChangeType(propValue, propType, CultureInfo.InvariantCulture);
        }

        return frostbiteType switch
        {
            TypeEnum.Boolean => ValueToPrimitive(bool.Parse(propValue), propType),
            TypeEnum.Float32 => ValueToPrimitive(float.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Float64 => ValueToPrimitive(double.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Int8 => ValueToPrimitive(sbyte.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Int16 => ValueToPrimitive(short.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Int32 => ValueToPrimitive(int.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Int64 => ValueToPrimitive(long.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.UInt8 => ValueToPrimitive(byte.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.UInt16 => ValueToPrimitive(ushort.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.UInt32 => ValueToPrimitive(uint.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.UInt64 => ValueToPrimitive(ulong.Parse(propValue, CultureInfo.InvariantCulture), propType),
            TypeEnum.Enum => Enum.Parse(propType, propValue),
            TypeEnum.Guid => ValueToPrimitive(Guid.Parse(propValue), propType),
            TypeEnum.FileRef => ValueToPrimitive(new FileRef(propValue), propType),
            TypeEnum.String or TypeEnum.CString => ValueToPrimitive(propValue, propType),
            TypeEnum.Class => new PointerRef(),
            TypeEnum.ResourceRef => ValueToPrimitive(
                new ResourceRef(ulong.Parse(propValue,
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
                propType),
            _ => throw new NotImplementedException("Not implemented property type: " + frostbiteType)
        };
    }

    private static string? GetAttributeValue(XmlNode node, string name)
    {
        return node.Attributes?.GetNamedItem(name)?.Value;
    }

    // Converts the given value to the given Frostbite primitive type (an int will be converted to Frostbite.Core.Int32)
    private static object ValueToPrimitive(object value, Type valueType)
    {
        IPrimitive primitive = (IPrimitive)Activator.CreateInstance(valueType)!;
        primitive.FromActualType(value);
        return primitive;
    }

    private void ReadPartition(XmlNode partitionNode)
    {
        Guid partitionGuid = Guid.Parse(GetAttributeValue(partitionNode, "guid")!);
        // (Never used) m_primaryInstGuid = Guid.Parse(GetAttributeValue(partitionNode, "primaryInstance")!);

        _ebx!.PartitionGuid = partitionGuid;

        foreach(XmlNode child in partitionNode.ChildNodes)
        {
            CreateInstance(child);
        }

        // Because of PointerRefs, every instance has to exist before any field is read
        foreach(var kvp in _guidToObjAndXmlNode)
        {
            ParseInstance(kvp.Value.dbxInstance, kvp.Value.ebxInstance);
        }

        _ebx!.InternalRefCounts = [.. _guidToRefCount.Values];
    }

    private void CreateInstance(XmlNode node)
    {
        Type? ebxType = TypeLibrary.GetType(GetAttributeValue(node, "type")!.Split('.')[^1])?.Type;
        if(ebxType is null)
        {
            return;
        }

        if (Activator.CreateInstance(ebxType) is not IEbxInstance obj)
        {
            return;
        }

        bool isExported = bool.Parse(GetAttributeValue(node, "exported")!);
        Guid instGuid = Guid.Parse(GetAttributeValue(node, "guid")!);

        AssetClassGuid assetGuid = new
            (isExported ? instGuid : Guid.Empty,
            ++_internalId);

        obj.SetInstanceGuid(assetGuid);
        _guidToObjAndXmlNode.Add(instGuid, (obj, node));
        _guidToRefCount.Add(instGuid, 0);
    }

    private void ParseInstance(XmlNode node, IEbxInstance obj)
    {
        ReadInstanceFields(node, obj, obj.GetType());
        _ebx!.AddObject(obj);
    }

    private PointerRef ParseRef(XmlNode node, string refGuid)
    {
        if (refGuid == "null")
        {
            return new PointerRef();
        }

        string? refEbxGuid = GetAttributeValue(node, "partitionGuid");
        // External
        if (refEbxGuid != null)
        {
            Guid extGuid = Guid.Parse(refGuid.Split('\\')[1]);
            Guid ebxFileGuid = Guid.Parse(refEbxGuid);
            EbxImportReference import = new() { InstanceGuid = extGuid, PartitionGuid = ebxFileGuid };
            _ebx!.AddDependency(ebxFileGuid);
            return new PointerRef(import);
        }

        // Internal
        Guid instGuid = Guid.Parse(refGuid);
        PointerRef internalRef = new(_guidToObjAndXmlNode[instGuid].ebxInstance);
        _guidToRefCount[instGuid]++;
        return internalRef;
    }

    private void ReadInstanceFields(XmlNode node, object obj, Type objType)
    {
        foreach(XmlNode child in node.ChildNodes)
        {
            ReadField(obj, child, objType);
        }
    }

    private void ReadField(object obj,
        XmlNode node,
        Type objType,
        bool isArray = false,
        bool isRef = false,
        Type? arrayElementType = null,
        TypeEnum? arrayElementTypeEnum = null)
    {
        switch (node.Name)
        {
            case "field":
            {
                string? refGuid = GetAttributeValue(node, "ref");
                if (refGuid is not null)
                {
                    SetProperty(obj, objType, GetAttributeValue(node, "name")!, ParseRef(node, refGuid));
                }
                else
                {
                    SetPropertyFromStringValue(obj, objType, GetAttributeValue(node, "name")!, node.InnerText);
                }
                break;
            }
            case "item":
            {
                if (isRef)
                {
                    ((IList)obj).Add(ParseRef(node, GetAttributeValue(node, "ref")!));
                }
                else
                {
                    ((IList)obj).Add(GetValueFromString(arrayElementType!, node.InnerText, arrayElementTypeEnum));
                }
                break;
            }
            case "array":
            {
                string arrayFieldName = GetAttributeValue(node, "name")!;
                object array = ReadArray(node);
                SetProperty(obj, objType, arrayFieldName, array);
                break;
            }
            case "complex":
            {
                string structFieldName = GetAttributeValue(node, "name")!;
                object structObj = ReadStruct(arrayElementType, node);
                if (isArray)
                {
                    ((IList)obj).Add(structObj);
                }
                else
                {
                    SetProperty(obj, objType, structFieldName, structObj);
                }
                break;
            }
            case "boxed":
            {
                BoxedValueRef boxed;

                string? typeName = GetAttributeValue(node, "typeName");
                if (typeName is not null)
                {
                    object? value;

                    IType? valueType = typeName == "PointerRef" ? new SdkType(typeof(PointerRef)) : TypeLibrary.GetType(typeName);
                    if (valueType is not SdkType)
                    {
                        break;
                    }

                    string? refGuid = GetAttributeValue(node, "ref");
                    if (refGuid is not null)
                    {
                        value = ParseRef(node, refGuid);
                    }
                    else
                    {
                        value = node.FirstChild?.Name switch
                        {
                            "complex" => ReadStruct(arrayElementType, node.FirstChild),
                            "array" => ReadArray(node.FirstChild),
                            _ => GetValueFromString(valueType.Type, node.InnerText, valueType.GetFlags().GetTypeEnum())
                        };
                    }

                    boxed = new BoxedValueRef(value, valueType.GetFlags());
                }
                else
                {
                    boxed = new BoxedValueRef();
                }
                SetProperty(obj, objType, GetAttributeValue(node, "name")!, ValueToPrimitive(boxed, BoxedValueRefType!));
                break;
            }
            case "typeref":
            {
                string? typeName = GetAttributeValue(node, "typeName");
                TypeRef typeRef = typeName is not null ? new TypeRef(TypeLibrary.GetType(typeName)) : new TypeRef();

                SetProperty(obj, objType, GetAttributeValue(node, "name")!, ValueToPrimitive(typeRef, TypeRefType!));
                break;
            }
            case "delegate":
            {
                string? typeName = GetAttributeValue(node, "typeName");
                IType? type = typeName is null ? null : TypeLibrary.GetType(typeName);
                PropertyInfo? property = objType.GetProperty(GetAttributeValue(node, "name")!, PropertyBindingFlags);
                if (property is not null && property.CanWrite)
                {
                    IDelegate del = (IDelegate)Activator.CreateInstance(property.PropertyType)!;
                    del.FunctionType = type;
                    property.SetValue(obj, del);
                }
                break;
            }
        }
    }

    private object ReadArray(XmlNode node)
    {
        string arrayTypeStr = GetAttributeValue(node, "type")!;
        bool isRef = arrayTypeStr.StartsWith("ref(", StringComparison.Ordinal);

        Type arrayElementType = (isRef ? PointerType : TypeLibrary.GetType(arrayTypeStr)?.Type)
                                ?? throw new ArgumentException($"Array element type doesn't exist? {arrayTypeStr}");

        EbxTypeMetaAttribute? arrayTypeMeta = arrayElementType.GetCustomAttribute<EbxTypeMetaAttribute>();
        Type arrayType = CollectionType.MakeGenericType(arrayElementType);

        object array = Activator.CreateInstance(arrayType)
                       ?? throw new ArgumentException($"Failed to create array with element type: {arrayElementType.Name}");

        foreach (XmlNode child in node.ChildNodes)
        {
            ReadField(array, child, arrayType, true, isRef, arrayElementType, arrayTypeMeta?.Flags.GetTypeEnum());
        }

        return array;
    }

    private object ReadStruct(Type? structType, XmlNode node)
    {
        Type type = (structType ?? TypeLibrary.GetType(GetAttributeValue(node, "type")!)?.Type)
                    ?? throw new ArgumentException("Struct type doesn't exist?");

        object obj = Activator.CreateInstance(type)
                      ?? throw new ArgumentException($"Failed to create struct of type: {type.Name}");

        foreach (XmlNode child in node.ChildNodes)
        {
            ReadField(obj, child, type);
        }

        return obj;
    }
}
