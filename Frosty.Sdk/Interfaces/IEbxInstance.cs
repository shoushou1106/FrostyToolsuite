using Frosty.Sdk.Ebx;

namespace Frosty.Sdk.Interfaces;

public interface IEbxInstance
{
    AssetClassGuid GetInstanceGuid();

    void SetInstanceGuid(AssetClassGuid newGuid);
}