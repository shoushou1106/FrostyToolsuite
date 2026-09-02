using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.IO.RiffEbx;

internal struct EbxExtra
{
    public uint Offset;
    public int Count;
    public uint Hash;
    public TypeFlags Flags;
    public ushort TypeDescriptorRef;
}