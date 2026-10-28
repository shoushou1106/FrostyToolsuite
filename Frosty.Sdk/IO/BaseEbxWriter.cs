using System.Collections;
using System.Reflection;
using System.Text;
using Frosty.Sdk.Attributes;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Ebx;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

public abstract class BaseEbxWriter(DataAccessor inAccessor)
{
    private protected readonly DataAccessor _accessor = inAccessor;
    private protected readonly List<IEbxInstance> _sortedInstances = [];

    private protected static readonly Type PointerType = typeof(PointerRef);
    private protected static readonly Type DataContainerType = TypeLibrary.GetType("DataContainer")!.Type;
    // ReSharper disable once MemberCanBePrivate.Global
    private protected static readonly Type? BoxedValueRefType = TypeLibrary.GetType("BoxedValueRef")?.Type;

    private protected const string EbxNamespace = "Frostbite";
    private protected const string CollectionName = "ObservableCollection`1";

    private protected uint _stringsLength;
    private protected readonly List<string> _strings = [];
    private readonly Dictionary<string, uint> _stringOffsets = new(StringComparer.Ordinal);

    private protected readonly Dictionary<uint, int> _typeToDescriptor = new();

    private protected readonly SortedSet<EbxImportReference> _imports = [];
    private protected readonly Dictionary<EbxImportReference, int> _importOrderFw = new();

    private protected readonly List<Block<byte>> _arrayData = [];
    private protected readonly List<Block<byte>> _boxedValueData = [];

    private protected readonly bool _useSharedTypeDescriptors = FileSystemManager.HasFileInMemoryFs("SharedTypeDescriptors.ebx");

    private readonly HashSet<object> _processedObjects = [];

    public static BaseEbxWriter CreateWriter(DataAccessor inAccessor)
    {
        return ProfilesLibrary.EbxVersion == 6 ? new RiffEbx.EbxWriter(inAccessor) : new LegacyEbx.EbxWriter(inAccessor);
    }

    public void WritePartition(EbxPartition inPartition)
    {
        ArgumentNullException.ThrowIfNull(inPartition);

        List<IEbxInstance> exportedInstances = new(1);
        List<IEbxInstance> internalInstances = new(inPartition.InternalInstances.Count);

        foreach (IEbxInstance instance in inPartition.InternalInstances)
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
        exportedInstances.Sort((a, b) =>
        {
        	AssetClassGuid guidA = a.GetInstanceGuid();
        	AssetClassGuid guidB = b.GetInstanceGuid();
        	if (guidA.ExportedGuid == inPartition.PrimaryInstanceGuid)
        	{
        		return 1;
        	}
        	return guidB.ExportedGuid == inPartition.PrimaryInstanceGuid ? -1 : guidA.ExportedGuid.CompareTo(guidB.ExportedGuid);
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
        InternalWriteEbx(inPartition.PartitionGuid, exportedInstanceCount);
    }

    protected abstract void InternalWriteEbx(Guid inPartitionGuid, int inExportedInstanceCount);

    protected abstract int CompareInstances(IEbxInstance inA, IEbxInstance inB);

    protected abstract int AddType(Type inType);

    private void ProcessType(Type inType, object inObj, bool ignore = false)
    {
        // Move real recursing into ProcessContainer()
        while (true)
        {
            if (!_processedObjects.Add(inObj))
            {
                return;
            }

            if (inType == PointerType)
            {
                PointerRef value = (PointerRef)inObj;
                switch (value.Type)
                {
                    case PointerRefType.Internal when value.Internal is not null:
                        inType = value.Internal.GetType();
                        inObj = value.Internal;
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

            if (inType == BoxedValueRefType)
            {
                BoxedValueRef boxedValueRef = (BoxedValueRef)((IPrimitive)inObj).ToActualType();
                if (boxedValueRef.Value is null)
                {
                    return;
                }

                inType = boxedValueRef.Value.GetType();
                inObj = boxedValueRef.Value;
                ignore = false;
                continue;
            }

            ProcessContainer(inType, inObj, ignore);
            return;
        }
    }

    private void ProcessContainer(Type inType, object inObj, bool ignore)
    {
        if (inType.IsAssignableTo(typeof(IPrimitive)))
        {
            // Other primitive types have no type descriptor
            return;
        }

        // Do not add the same type multiple times
        bool addType = FindExistingType(inType) == -1 && !ignore;

        if (inType.IsEnum)
        {
            if (addType)
            {
                AddType(inType);
            }
        }
        else if (inType.Name.Equals(CollectionName, StringComparison.Ordinal))
        {
            foreach (object o in (IList)inObj)
            {
                ProcessType(o.GetType(), o);
            }

            if (addType)
            {
                AddType(inType);
            }
        }
        else if (inType.IsClass)
        {
            if (addType)
            {
                AddType(inType);
            }

            ProcessProperties(inType.GetProperties(BindingFlags.Public | BindingFlags.Instance), inObj);
        }
        else if (inType.IsValueType)
        {
            if (addType)
            {
                AddType(inType);
            }

            ProcessProperties(inType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), inObj);
        }
    }

    private void ProcessProperties(PropertyInfo[] inProperties, object inObj)
    {
        foreach (PropertyInfo pi in inProperties)
        {
            // Ignore transients if saving to project
            if (pi.GetCustomAttribute<IsTransientAttribute>() is not null)
            {
                continue;
            }

            ProcessType(pi.PropertyType, pi.GetValue(inObj)!, _useSharedTypeDescriptors);
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

    private protected uint AddString(string stringToAdd)
    {
        // Dictionary<string, uint> _stringOffsets = new(StringComparer.Ordinal)
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

    protected int FindExistingType(Type inType)
    {
        uint hash = _useSharedTypeDescriptors ? inType.GetNameHash() : (uint)Fnv1.HashString(inType.GetName());

        return _typeToDescriptor.GetValueOrDefault(hash, -1);
    }

    protected int FindExistingType(IType inType)
    {
        ArgumentNullException.ThrowIfNull(inType);

        uint hash = _useSharedTypeDescriptors ? inType.NameHash : (uint)Fnv1.HashString(inType.Name);

        return _typeToDescriptor.GetValueOrDefault(hash, -1);
    }
}