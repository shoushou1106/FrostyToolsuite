namespace Frosty.Sdk.IO.LegacyEbx;

internal struct EbxInstance
{
    public ushort TypeDescriptorRef;
    public ushort Count;
    
    public bool IsExported;
}