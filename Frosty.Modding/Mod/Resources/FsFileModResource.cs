using Frosty.Sdk.IO;

namespace Frosty.Modding.Mod.Resources;

public class FsFileModResource : BaseModResource
{
    public override ModResourceType Type => ModResourceType.FsFile;
    
    public FsFileModResource(DataStream inStream)
        : base(inStream)
    {
    }
}