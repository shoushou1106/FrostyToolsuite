using System.Collections.Generic;

using Frosty.Modding.Mod;
using Frosty.Modding.Mod.Resources;

namespace Frosty.Modding.Interfaces;

public interface IResourceContainer
{
    /// <summary>
    /// The Resources of this resource container.
    /// </summary>
    public IEnumerable<BaseModResource> Resources { get; }
    
    /// <summary>
    /// Gets the data of a resource
    /// </summary>
    /// <param name="inIndex">The index of the resource.</param>
    /// <returns></returns>
    public ResourceData GetData(int inIndex);
}