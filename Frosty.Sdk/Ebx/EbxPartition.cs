using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;

namespace Frosty.Sdk.Ebx;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
public class EbxPartition
{
    public EbxPartition()
    {
    }

    public EbxPartition(params IEbxInstance[] rootObjects)
    {
        ArgumentNullException.ThrowIfNull(rootObjects);

        PartitionGuid = Guid.NewGuid();

        foreach (IEbxInstance obj in rootObjects)
        {
            obj.SetInstanceGuid(new AssetClassGuid(Guid.NewGuid(), InternalInstances.Count));
            InternalInstances.Add(obj);
        }
    }

    public Guid PartitionGuid { get; internal set; }

    public Guid PrimaryInstanceGuid => PrimaryInstance.GetInstanceGuid().ExportedGuid;

    public IEnumerable<Guid> Dependencies => InternalDependencies;

    public IEnumerable<IEbxInstance> RootInstances =>
        InternalInstances.Where((_, i) => InternalRefCounts[i] == 0 || i == 0);

    public IEnumerable<IEbxInstance> Instances => InternalInstances;

    // Public API, remove warning disbale after something called it.
    // ReSharper disable once MemberCanBePrivate.Global
    public IEnumerable<IEbxInstance> ExportedObjects =>
        InternalInstances.Where(obj => obj.GetInstanceGuid().IsExported);

    public IEbxInstance PrimaryInstance => InternalInstances[0];

    public bool IsValid => InternalInstances.Count != 0;

    internal List<IEbxInstance> InternalInstances { get; set; } = [];
    internal List<int> InternalRefCounts { get; set; } = [];
    internal HashSet<Guid> InternalDependencies { get; set; } = [];

    public static EbxPartition Deserialize(DataStream ebxStream)
    {
        BaseEbxReader reader = BaseEbxReader.CreateReader(ebxStream);
        return reader.ReadPartition<EbxPartition>();
    }

    public static void Serialize(DataStream ebxStream, EbxPartition inPartition)
    {
        BaseEbxWriter writer = BaseEbxWriter.CreateWriter(ebxStream);
        writer.WritePartition(inPartition);
    }

    /// <summary>
    ///     Invoked when loading of the ebx asset has completed, to allow for any custom handling
    /// </summary>
    public virtual void OnLoadComplete()
    {
    }

    public IEbxInstance? GetObject(Guid exportedGuid)
    {
        return ExportedObjects.FirstOrDefault(obj => obj.GetInstanceGuid() == exportedGuid);
    }

    public bool AddDependency(Guid dependencyGuid)
    {
        return InternalDependencies.Add(dependencyGuid);
    }

    public void SetFileGuid(Guid fileGuid)
    {
        PartitionGuid = fileGuid;
    }

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
    }

    public void RemoveObject(IEbxInstance obj)
    {
        int idx = InternalInstances.IndexOf(obj);
        if (idx == -1)
        {
            return;
        }

        InternalInstances.RemoveAt(idx);
    }
}