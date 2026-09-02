using System;
using System.Collections.Generic;

namespace Frosty.Modding.ModInfos;

public class SuperBundleModAction
{
    public Dictionary<int, BundleModInfo> Bundles = new();
    public HashSet<Guid> Chunks = new();
}