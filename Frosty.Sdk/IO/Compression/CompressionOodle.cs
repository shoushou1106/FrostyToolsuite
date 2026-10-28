using System.Reflection;
using System.Runtime.InteropServices;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.IO.Compression;

/// <summary>
///     Oodle Kraken, Selkie, and Leviathan through the Oodle library in <c>ThirdParty/</c>,
///     <see cref="Managers.ResourceManager" /> manage download for games that use Oodle.
/// </summary>
public sealed partial class CompressionOodle : ICompressionFormat
{
    /// <summary>
    ///     Where Oodle is loaded from
    /// </summary>
    internal static string LibraryPath { get; } = Path.Combine(AppContext.BaseDirectory, "ThirdParty",
        OperatingSystem.IsWindows() ? "liboo2core.dll" :
        OperatingSystem.IsMacOS() ? "liboo2core.dylib" : "liboo2core.so");

    public string Identifier => "Oodle";

    public int Decompress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        nint written = NativeMethods.OodleLZ_Decompress(inData, inData.Length, outData, outData.Length,
            OodleFuzzSafe.Yes, OodleCheckCrc.No, OodleVerbosity.None, 0, 0, 0, 0, 0, 0,
            OodleDecodeThreadPhase.Unthreaded);

        return written > 0 ? (int)written : throw new InvalidDataException("An Oodle block could not be decompressed.");
    }

    public int Compress(ReadOnlySpan<byte> inData, Span<byte> outData,
        CompressionOptions inOptions = CompressionOptions.None)
    {
        // TODO:
        // Encode for the game's own Oodle version.
        // The game decompresses with its own oo2core library,
        // which can be older than 2.9 and is not guaranteed to read what 2.9 encodes.
        // Oodle_SetConfigValues() with m_OodleLZ_BackwardsCompatible_MajorVersion
        // set to the game's major version fixes it.
        // Until then the output is 2.9.16 (Major 9).
        nint written = NativeMethods.OodleLZ_Compress(GetCompressor(inOptions), inData, inData.Length, outData,
            OodleCompressionLevel.Optimal5, 0, 0, 0, 0, 0);

        return written > 0 ? (int)written : throw new InvalidOperationException("Oodle failed to compress the data.");
    }

    public int GetCompressBounds(int inRawSize, CompressionOptions inOptions = CompressionOptions.None)
    {
        return checked((int)NativeMethods.OodleLZ_GetCompressedBufferSizeNeeded(GetCompressor(inOptions), inRawSize));
    }

    private static OodleCompressor GetCompressor(CompressionOptions inOptions)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return inOptions switch
        {
            CompressionOptions.OodleKraken => OodleCompressor.Kraken,
            CompressionOptions.OodleSelkie => OodleCompressor.Selkie,
            CompressionOptions.OodleLeviathan => OodleCompressor.Leviathan,
            _ => throw new ArgumentOutOfRangeException(nameof(inOptions), inOptions, "Not an Oodle compressor.")
        };
    }

    /// <remarks>
    ///     <para>
    ///     Signatures and enum values follow
    ///     <see href="https://github.com/WorkingRobot/OodleUE/blob/2026-06-04-1357/Engine/Source/Runtime/OodleDataCompression/Sdks/2.9.16/include/oodle2.h">oodle2.h</see>
    ///     of Oodle 2.9.16, the version that gets downloaded.
    ///     Add enum values from there only when needed.
    ///     </para>
    ///     <para><c>OO_SINTa</c> is <see cref="nint" />.</para>
    ///     <para>Modern .NET LibraryImport resolves Span{T} automatically with memory pinning.</para>
    /// </remarks>
    private static partial class NativeMethods
    {
        private const string LibraryName = "oo2core";

        static NativeMethods()
        {
            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveLibrary);
        }

        [LibraryImport(LibraryName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint OodleLZ_Decompress(ReadOnlySpan<byte> compBuf, nint compBufSize,
            Span<byte> rawBuf, nint rawLen, OodleFuzzSafe fuzzSafe, OodleCheckCrc checkCrc,
            OodleVerbosity verbosity, nint decBufBase, nint decBufSize, nint fpCallback, nint callbackUserData,
            nint decoderMemory, nint decoderMemorySize, OodleDecodeThreadPhase threadPhase);

        [LibraryImport(LibraryName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint OodleLZ_Compress(OodleCompressor compressor, ReadOnlySpan<byte> rawBuf,
            nint rawLen, Span<byte> compBuf, OodleCompressionLevel level, nint pOptions, nint dictionaryBase,
            nint lrm, nint scratchMem, nint scratchSize);

        [LibraryImport(LibraryName)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint OodleLZ_GetCompressedBufferSizeNeeded(OodleCompressor compressor,
            nint rawSize);

        /// <summary>
        ///     Loads Oodle from the one place it is downloaded to,
        ///     instead of letting the OS search for it. (macOS fails on relative paths)
        ///     Every other library of this assembly falls back to the default lookup.
        /// </summary>
        private static nint ResolveLibrary(string inLibraryName, Assembly inAssembly, DllImportSearchPath? inSearchPath)
        {
            if (inLibraryName != LibraryName)
            {
                return 0;
            }

            return NativeLibrary.TryLoad(LibraryPath, out nint handle) ? handle : 0;
        }
    }

    private enum OodleFuzzSafe
    {
        Yes = 1
    }

    private enum OodleCheckCrc
    {
        No = 0
    }

    private enum OodleVerbosity
    {
        None = 0
    }

    private enum OodleDecodeThreadPhase
    {
        Unthreaded = 3
    }

    private enum OodleCompressor
    {
        Kraken = 8,
        Selkie = 11,
        Leviathan = 13
    }

    private enum OodleCompressionLevel
    {
        Optimal5 = 9
    }
}
