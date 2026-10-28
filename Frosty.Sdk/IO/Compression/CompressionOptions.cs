namespace Frosty.Sdk.IO.Compression;

[Flags]
public enum CompressionOptions
{
    None = 0,
    ZstdWithDict = 1 << 0,
    OodleKraken = 1 << 1,
    OodleSelkie = 1 << 2,
    OodleLeviathan = 1 << 3
}