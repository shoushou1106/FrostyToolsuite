namespace Frosty.Sdk.IO.Compression;

/// <summary>
///     The compression of a CAS block.
/// </summary>
/// <remarks>
/// The low 7 bits are the format, bit 7 is the <see cref="Obfuscated" /> flag.
/// </remarks>
// CA1028: Enum storage should be Int32
// CA2217: Do not mark enums with FlagsAttribute
// Only Obfuscated is a flag, callers mask it out with & ~Obfuscated
#pragma warning disable CA1028, CA2217
[Flags]
public enum CompressionType : byte
#pragma warning restore CA1028, CA2217
{
    None = 0x00,
    ZLib = 0x02,
    Lz4 = 0x09,
    Zstd = 0x0F,
    OodleKraken = 0x11,
    OodleSelkie = 0x15,
    OodleLeviathan = 0x19,

    Obfuscated = 1 << 7
}
