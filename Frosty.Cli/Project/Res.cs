using Frosty.Sdk.Managers.Entries;

namespace Frosty.Cli.Project;

public class Res : Asset
{
    public ResourceType ResType { get; set; }
    public ulong ResRid { get; set; }
}