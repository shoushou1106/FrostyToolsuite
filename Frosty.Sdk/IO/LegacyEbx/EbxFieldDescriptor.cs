using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.IO.LegacyEbx;

internal struct EbxFieldDescriptor
{
    public string Name;
    public uint NameHash;
    public TypeFlags Flags;
    public ushort TypeDescriptorRef;
    public uint DataOffset;
    public uint SecondOffset;
}