using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.IO.RiffEbx;

internal struct EbxFieldDescriptor
{
    public uint NameHash;
    public uint DataOffset;
    public TypeFlags Flags;
    public ushort TypeDescriptorRef;
}