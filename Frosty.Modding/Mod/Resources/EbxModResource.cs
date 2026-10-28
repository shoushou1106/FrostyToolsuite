using System.Collections.Generic;
using Frosty.Sdk.Utils;
using Frosty.Sdk.IO;

namespace Frosty.Modding.Mod.Resources;

public class EbxModResource : BaseModResource
{
    public override ModResourceType Type => ModResourceType.Ebx;

    public EbxModResource(DataAccessor inAccessor)
        : base(inAccessor)
    {
    }

    public EbxModResource(int inResourceIndex, string inName, Sha1 inSha1, long inOriginalSize,
        ResourceFlags inFlags, int inHandlerHash, string inUserData, IEnumerable<int> inBundlesToAdd,
        IEnumerable<int> inBundlesToRemove)
        : base(inResourceIndex, inName, inSha1, inOriginalSize, inFlags, inHandlerHash,
            inUserData, inBundlesToAdd, inBundlesToRemove)
    {
    }
}