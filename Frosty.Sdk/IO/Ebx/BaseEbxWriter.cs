using System.Collections;
using System.Reflection;
using System.Text;

using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.RiffEbx;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO.Ebx;

public abstract class BaseEbxWriter(DataAccessor accessor)
{
    private protected const string EbxNamespace = "Frostbite";
    private protected const string CollectionName = "ObservableCollection`1";

    private protected static readonly Type PointerType = typeof(PointerRef);
    private protected static readonly Type DataContainerType = TypeLibrary.GetType("DataContainer")!.Type;

    private protected static readonly Type? BoxedValueRefType = TypeLibrary.GetType("BoxedValueRef")?.Type;
    private protected readonly DataAccessor _accessor = accessor;

    private protected readonly List<byte[]> _arrayData = [];
    private protected readonly List<byte[]> _boxedValueData = [];
    private protected readonly Dictionary<EbxImportReference, int> _importOrderFw = [];

    private protected readonly SortedSet<EbxImportReference> _imports = [];

    private readonly HashSet<object> _processedObjects = [];
    private protected readonly List<IEbxInstance> _sortedInstances = [];
    private readonly Dictionary<string, uint> _stringOffsets = new(StringComparer.Ordinal);
    private protected readonly List<string> _strings = [];

    private protected readonly Dictionary<uint, int> _typeToDescriptor = [];

    private protected readonly bool _useSharedTypeDescriptors =
        FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors.ebx");

    private protected uint _stringsLength;

    public static BaseEbxWriter CreateWriter(DataAccessor accessor)
    {
        return ProfilesLibrary.EbxVersion == 6 ? new EbxWriter(accessor) : new LegacyEbx.EbxWriter(accessor);
    }

    public void WritePartition(EbxPartition partition)
    {
        ArgumentNullException.ThrowIfNull(partition);

        List<IEbxInstance> exportedInstances = new(1);
        List<IEbxInstance> internalInstances = new(partition.InternalInstances.Count);

        foreach (IEbxInstance instance in partition.InternalInstances)
        {
            if (instance.GetInstanceGuid().IsExported)
            {
                exportedInstances.Add(instance);
            }
            else
            {
                internalInstances.Add(instance);
            }
        }

        int exportedInstanceCount = exportedInstances.Count;
        // The primary instance goes last, the others by GUID
        Guid primaryInstanceGuid = partition.PrimaryInstanceGuid;
        exportedInstances.Sort((a, b) =>
        {
            Guid guidA = a.GetInstanceGuid().ExportedGuid;
            Guid guidB = b.GetInstanceGuid().ExportedGuid;
            bool isPrimaryA = guidA == primaryInstanceGuid;
            bool isPrimaryB = guidB == primaryInstanceGuid;

            return isPrimaryA != isPrimaryB ? isPrimaryA.CompareTo(isPrimaryB) : guidA.CompareTo(guidB);
        });

        internalInstances.Sort(CompareInstances);

        _sortedInstances.AddRange(exportedInstances);
        _sortedInstances.AddRange(internalInstances);

        foreach (Type type in _sortedInstances.Select(static instance => instance.GetType()))
        {
            if (FindExistingType(type) == -1)
            {
                AddType(type);
            }
        }

        foreach (IEbxInstance instance in _sortedInstances)
        {
            ProcessType(instance.GetType(), instance);
        }

        GenerateImportOrder();
        InternalWriteEbx(partition.PartitionGuid, exportedInstanceCount);
    }

    protected abstract void InternalWriteEbx(Guid partitionGuid, int exportedInstanceCount);

    protected abstract int CompareInstances(IEbxInstance a, IEbxInstance b);

    protected abstract int AddType(Type type);

    private void ProcessType(Type type, object obj, bool ignore = false)
    {
        // Move real recursing into ProcessContainer()
        while (true)
        {
            if (!_processedObjects.Add(obj))
            {
                return;
            }

            if (type == PointerType)
            {
                PointerRef value = (PointerRef)obj;
                switch (value.Type)
                {
                    case PointerRefType.Internal when value.Internal is not null:
                        type = value.Internal.GetType();
                        obj = value.Internal;
                        ignore = false;
                        continue;
                    case PointerRefType.External:
                        _imports.Add(value.External);
                        return;
                    case PointerRefType.Null:
                    case PointerRefType.Internal:
                    default:
                        return;
                }
            }

            if (type == BoxedValueRefType)
            {
                BoxedValueRef boxedValueRef = (BoxedValueRef)((IPrimitive)obj).ToActualType();
                if (boxedValueRef.Value is null)
                {
                    return;
                }

                type = boxedValueRef.Value.GetType();
                obj = boxedValueRef.Value;
                ignore = false;
                continue;
            }

            ProcessContainer(type, obj, ignore);
            return;
        }
    }

    private void ProcessContainer(Type type, object obj, bool ignore)
    {
        if (type.IsAssignableTo(typeof(IPrimitive)))
        {
            // Other primitive types have no type descriptor
            return;
        }

        // Do not add the same type multiple times
        bool addType = FindExistingType(type) == -1 && !ignore;

        if (type.IsEnum)
        {
            if (addType)
            {
                AddType(type);
            }
        }
        else if (type.Name.Equals(CollectionName, StringComparison.Ordinal))
        {
            foreach (object o in (IList)obj)
            {
                ProcessType(o.GetType(), o);
            }

            if (addType)
            {
                AddType(type);
            }
        }
        else if (type.IsClass)
        {
            if (addType)
            {
                AddType(type);
            }

            ProcessProperties(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), obj);
        }
        else if (type.IsValueType)
        {
            if (addType)
            {
                AddType(type);
            }

            ProcessProperties(type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), obj);
        }
    }

    private void ProcessProperties(PropertyInfo[] properties, object obj)
    {
        foreach (PropertyInfo pi in properties)
        {
            // Ignore transients if saving to project
            if (pi.GetCustomAttribute<IsTransientAttribute>() is not null)
            {
                continue;
            }

            ProcessType(pi.PropertyType, pi.GetValue(obj)!, _useSharedTypeDescriptors);
        }
    }

    private void GenerateImportOrder()
    {
        int iter = 0;
        foreach (EbxImportReference import in _imports)
        {
            _importOrderFw[import] = iter;
            iter++;
        }
    }

    /// <summary>
    ///     Gets what was written to <paramref name="stream" />,
    ///     zero padded up to its position.
    /// </summary>
    /// <remarks>A field that is skipped at the end of the data still takes up its bytes.</remarks>
    private protected static byte[] GetWrittenBytes(MemoryStream stream)
    {
        if (stream.Position > stream.Length)
        {
            stream.SetLength(stream.Position);
        }

        return stream.ToArray();
    }

    private protected uint AddString(string stringToAdd)
    {
        if (_stringOffsets.TryGetValue(stringToAdd, out uint offset))
        {
            return offset;
        }

        offset = _stringsLength;
        _strings.Add(stringToAdd);
        _stringOffsets.Add(stringToAdd, offset);
        _stringsLength += (uint)(Encoding.UTF8.GetByteCount(stringToAdd) + 1);
        return offset;
    }

    protected int FindExistingType(Type type)
    {
        uint hash = _useSharedTypeDescriptors ? type.GetNameHash() : (uint)Fnv1.HashString(type.GetName());

        return _typeToDescriptor.GetValueOrDefault(hash, -1);
    }

    protected int FindExistingType(IType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        uint hash = _useSharedTypeDescriptors ? type.NameHash : (uint)Fnv1.HashString(type.Name);

        return _typeToDescriptor.GetValueOrDefault(hash, -1);
    }
}