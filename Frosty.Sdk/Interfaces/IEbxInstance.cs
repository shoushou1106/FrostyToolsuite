using Frosty.Sdk.Ebx;

namespace Frosty.Sdk.Interfaces;

/// <summary>
///     An instance in an EBX partition, implemented by the generated SDK classes.
/// </summary>
/// TODO: Merge to AssetClassGuid InstanceGuid { get; set; }
public interface IEbxInstance
{
    /// <summary>
    ///     Gets the GUID of the instance in its partition.
    /// </summary>
    AssetClassGuid GetInstanceGuid();

    /// <summary>
    ///     Sets the GUID of the instance in its partition.
    /// </summary>
    void SetInstanceGuid(AssetClassGuid newGuid);
}
