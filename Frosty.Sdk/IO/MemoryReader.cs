using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Frosty.Sdk.Utils;

using Microsoft.Win32.SafeHandles;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads the memory of another process.
///     <c>ReadProcessMemory</c> on Windows,
///     <c>/proc/&lt;pid&gt;/mem</c> on Linux.
///     Both need the same rights as attaching a debugger to that process.
/// </summary>
public sealed partial class MemoryReader(Process inProcess) : IDisposable
{
    // Linux only, lazy load.
    private SafeFileHandle? _memoryHandle;

    public long Position { get; set; }

    public void Dispose()
    {
        _memoryHandle?.Dispose();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void Pad(int alignment)
    {
        if (Position % alignment != 0)
        {
            Position += alignment - (Position % alignment);
        }
    }

    public byte ReadByte()
    {
        Span<byte> buffer = stackalloc byte[sizeof(byte)];
        ReadExactly(buffer);
        return buffer[0];
    }

    public short ReadShort(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(short));
        }

        Span<byte> buffer = stackalloc byte[sizeof(short)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    public ushort ReadUShort(bool pad = true)
    {
        return (ushort)ReadShort(pad);
    }

    public int ReadInt(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(int));
        }

        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public uint ReadUInt(bool pad = true)
    {
        return (uint)ReadInt(pad);
    }

    public long ReadLong(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(long));
        }

        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public ulong ReadULong(bool pad = true)
    {
        return (ulong)ReadLong(pad);
    }

    public float ReadSingle(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(float));
        }

        Span<byte> buffer = stackalloc byte[sizeof(float)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public double ReadDouble(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(double));
        }

        Span<byte> buffer = stackalloc byte[sizeof(double)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public Guid ReadGuid(bool pad = true)
    {
        if (pad)
        {
            Pad(4);
        }

        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Guid>()];
        ReadExactly(buffer);

        return new Guid(buffer);
    }

    public Sha1 ReadSha1()
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<Sha1>()];
        ReadExactly(buffer);

        return new Sha1(buffer);
    }

    /// <summary>
    ///     Reads a pointer to a null-terminated string and the string it points to.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (all languages).</remarks>
    public string ReadUtf8StringFromPointer(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(long));
        }

        long offset = ReadLong();
        long orig = Position;
        Position = offset;

        List<byte> bytes = [];
        for (byte b = ReadByte(); b != 0; b = ReadByte())
        {
            bytes.Add(b);
        }

        Position = orig;
        return Encoding.UTF8.GetString(CollectionsMarshal.AsSpan(bytes));
    }

    public void ReadExactly(Span<byte> buffer)
    {
        int bytesRead = ReadMemory((nint)Position, buffer);
        if (bytesRead != buffer.Length)
        {
            throw new EndOfStreamException();
        }

        Position += bytesRead;
    }

    public nint ScanPattern(string inPattern)
    {
        ArgumentNullException.ThrowIfNull(inPattern);

        string[] parts = inPattern.Trim().Split(' ');
        byte[] pattern = new byte[parts.Length];
        bool[] isWildcard = new bool[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            isWildcard[i] = parts[i].Contains('?', StringComparison.Ordinal);
            pattern[i] = isWildcard[i]
                ? (byte)0
                : byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        foreach ((nint address, int size) in GetRegions())
        {
            using Block<byte> region = new(size);
            ReadMemory(address, region);

            int index = IndexOfPattern(region, pattern, isWildcard);
            if (index != -1)
            {
                return address + index;
            }
        }

        return nint.Zero;
    }

    private static int IndexOfPattern(ReadOnlySpan<byte> inBuffer,
        ReadOnlySpan<byte> inPattern, ReadOnlySpan<bool> inIsWildcard)
    {
        for (int i = 0; i <= inBuffer.Length - inPattern.Length; i++)
        {
            int x = 0;
            while (x < inPattern.Length && (inIsWildcard[x] || inBuffer[i + x] == inPattern[x]))
            {
                x++;
            }

            if (x == inPattern.Length)
            {
                return i;
            }
        }

        return -1;
    }

    private IEnumerable<(nint Address, int Size)> GetRegions()
    {
        if (OperatingSystem.IsWindows())
        {
            nint currentAddress = inProcess.MainModule?.BaseAddress ?? 0;

            while (true)
            {
                if (NativeMethods.VirtualQueryEx(inProcess.SafeHandle, currentAddress,
                        out MemoryBasicInformation64 region, (nuint)Unsafe.SizeOf<MemoryBasicInformation64>()) == 0)
                {
                    int error = Marshal.GetLastPInvokeError();
                    if (error == ErrorInvalidParameter)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                if (region.State.HasFlag(AllocationType.Commit) &&
                    region.Protect != ProtectionType.NoAccess &&
                    !region.Protect.HasFlag(ProtectionType.Guard))
                {
                    yield return (currentAddress, (int)region.RegionSize);
                }

                currentAddress = (nint)region.BaseAddress + (nint)region.RegionSize;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            foreach (string region in File.ReadLines($"/proc/{inProcess.Id}/maps"))
            {
                string[] columns = region.Split(' ');
                string range = columns[0];
                int separator = range.IndexOf('-', StringComparison.Ordinal);
                nint start = nint.Parse(range.AsSpan(0, separator), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                nint end = nint.Parse(range.AsSpan(separator + 1), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);

                string permissions = columns[1];
                if (permissions[0] == '-'
                    // || permissions[1] == '-'
                    || permissions[2] != 'x')
                {
                    continue;
                }

                yield return (start, (int)(end - start));
            }
        }
        else
        {
            throw new PlatformNotSupportedException("MemoryReader only supports Windows or Linux right now.");
        }
    }

    private int ReadMemory(nint inAddress, Span<byte> outData)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!NativeMethods.ReadProcessMemory(inProcess.SafeHandle, inAddress, outData, (nuint)outData.Length,
                    out nuint bytesRead))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return (int)bytesRead;
        }

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("MemoryReader only supports Windows or Linux right now.");
        }

        _memoryHandle ??= File.OpenHandle($"/proc/{inProcess.Id}/mem", FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite);
        return RandomAccess.Read(_memoryHandle, outData, inAddress);
    }

    #region -- Windows --

    private const int ErrorInvalidParameter = 0x57;

    [Flags]
    private enum AllocationType : uint
    {
        Commit = 0x1000
    }

    [Flags]
    private enum ProtectionType : uint
    {
        NoAccess = 0x1,
        Guard = 0x100
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private readonly record struct MemoryBasicInformation64(
        [field: FieldOffset(0x0)] long BaseAddress,
        [field: FieldOffset(0x18)] nuint RegionSize,
        [field: FieldOffset(0x20)] AllocationType State,
        [field: FieldOffset(0x24)] ProtectionType Protect);

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ReadProcessMemory(SafeProcessHandle hProcess, nint lpBaseAddress,
            Span<byte> lpBuffer, nuint nSize, out nuint lpNumberOfBytesRead);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial nuint VirtualQueryEx(SafeProcessHandle hProcess, nint lpAddress,
            out MemoryBasicInformation64 lpBuffer, nuint dwLength);
    }

    #endregion
}