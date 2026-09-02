using Frosty.Sdk;

namespace Frosty.Modding.Interfaces;

public interface IModEntry
{
    public Sha1 Sha1 { get; }

    public long OriginalSize { get; }

    public IHandler? Handler { get; set; }
}