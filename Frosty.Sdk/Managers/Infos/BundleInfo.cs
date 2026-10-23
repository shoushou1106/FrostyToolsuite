using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Managers.Infos;

public class BundleInfo
{
    public string Name { get; }
    
    public int Id { get; }
    
    public SuperBundleInstallChunk Parent { get; }
    
    // public BundleType Type { get; internal set; }

    public BundleInfo(string inName, SuperBundleInstallChunk inParent)
    {
        Name = inName;
        Parent = inParent;
        Id = Fnv1.HashString(Name + Parent.Name, true);
        Parent.BundleMapping.Add(Name, this);
    }
}