using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.IO.Ebx;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     The instances of one EBX asset, the first is the primary instance.
/// </summary>
// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
public class EbxPartition
{
    public EbxPartition()
    {
    }

    /// <summary>
    ///     Creates a partition with a new GUID that contains <paramref name="rootObjects" />, with new exported GUIDs.
    /// </summary>
    public EbxPartition(params IEbxInstance[] rootObjects)
    {
        ArgumentNullException.ThrowIfNull(rootObjects);

        PartitionGuid = Guid.NewGuid();

        foreach (IEbxInstance obj in rootObjects)
        {
            obj.SetInstanceGuid(new AssetClassGuid(Guid.NewGuid(), InternalInstances.Count));
            InternalInstances.Add(obj);
            InternalRefCounts.Add(0);
        }
    }

    /// <summary>
    ///     The GUID of the EBX asset, which other partitions use to reference it.
    /// </summary>
    /// <remarks>Set a new GUID when the partition is copied to a new asset.</remarks>
    public Guid PartitionGuid { get; set; }

    public Guid PrimaryInstanceGuid => PrimaryInstance.GetInstanceGuid().ExportedGuid;

    public IEnumerable<Guid> Dependencies => InternalDependencies;

    /// <summary>
    ///     The primary instance and the instances that no other instance points to.
    /// </summary>
    public IEnumerable<IEbxInstance> RootInstances =>
        InternalInstances.Where((_, i) => InternalRefCounts[i] == 0 || i == 0);

    public IEnumerable<IEbxInstance> Instances => InternalInstances;

    public IEnumerable<IEbxInstance> ExportedObjects =>
        InternalInstances.Where(obj => obj.GetInstanceGuid().IsExported);

    public IEbxInstance PrimaryInstance => InternalInstances[0];

    public bool IsValid => InternalInstances.Count != 0;

    internal List<IEbxInstance> InternalInstances { get; set; } = [];

    /// <summary>
    ///     For each instance in <see cref="InternalInstances" />, how many other instances point to it.
    /// </summary>
    internal List<int> InternalRefCounts { get; set; } = [];

    internal HashSet<Guid> InternalDependencies { get; set; } = [];

    public static EbxPartition Deserialize(DataAccessor ebxAccessor)
    {
        BaseEbxReader reader = BaseEbxReader.CreateReader(ebxAccessor);
        return reader.ReadPartition<EbxPartition>();
    }

    public static void Serialize(DataAccessor ebxAccessor, EbxPartition partition)
    {
        BaseEbxWriter writer = BaseEbxWriter.CreateWriter(ebxAccessor);
        writer.WritePartition(partition);
    }

    /// <summary>
    ///     Invoked when loading of the EBX asset has completed, to allow for any custom handling.
    /// </summary>
    public virtual void OnLoadComplete()
    {
    }

    /// <returns>The exported instance with <paramref name="exportedGuid" />, or <see langword="null" />.</returns>
    public IEbxInstance? GetObject(Guid exportedGuid)
    {
        return ExportedObjects.FirstOrDefault(obj => obj.GetInstanceGuid().ExportedGuid == exportedGuid);
    }

    public bool AddDependency(Guid dependencyGuid)
    {
        return InternalDependencies.Add(dependencyGuid);
    }

    /// <summary>
    ///     Adds an instance that nothing points to yet, and sets its internal ID if not set.
    /// </summary>
    public void AddObject(IEbxInstance obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        AssetClassGuid guid = obj.GetInstanceGuid();
        if (guid.InternalId == -1)
        {
            // Make sure internal ID is set before adding
            guid = new AssetClassGuid(guid.ExportedGuid, InternalInstances.Count);
            obj.SetInstanceGuid(guid);
        }

        InternalInstances.Add(obj);
        InternalRefCounts.Add(0);
    }

    /// <summary>
    ///     Removes an instance
    /// </summary>
    /// <remarks>
    ///     The internal IDs of the other instances are kept, so they can have gaps.
    ///     Maybe fine, because the writers only use them to sort the instances.
    /// </remarks>
    /// <returns><see langword="true" /> if <paramref name="obj" /> was in the partition.</returns>
    public bool RemoveObject(IEbxInstance obj)
    {
        int index = InternalInstances.IndexOf(obj);
        if (index == -1)
        {
            return false;
        }

        // TODO:
        // The RemoveObject method does not work the same way as v1 does.
        // v1 calls heavy Update() everytime after remove,
        // which rebuild the whole data tree, and we don't.
        // Warning: Below is written by AI
        // The instances that obj points to keep their reference counts,
        // so one that only obj pointed to is still not in RootInstances,
        // and PointerRefs to obj in other instances are kept.
        // Fixing it means walking the fields of obj the way the readers count references.
        // Decide it together with the delete command of the editor.
        InternalInstances.RemoveAt(index);
        InternalRefCounts.RemoveAt(index);
        return true;
    }
}