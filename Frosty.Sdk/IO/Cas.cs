using System.Buffers;
using System.Diagnostics;
using System.Globalization;

using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Compression;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Profiles;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads and writes Frostbite CAS data.
/// </summary>
/// <remarks>
///     CAS data is stored in blocks of at most <see cref="ProfilesLibrary.MaxBufferSize" /> bytes.
///     Each block is individually compressed and preceded by an 8-byte big-endian header.
/// </remarks>
public static class Cas
{
    private static readonly CompressionZLib ZLibFormat = new();
    private static readonly CompressionZstd ZstdFormat = new();
    private static readonly CompressionLz4 Lz4Format = new();
    private static readonly CompressionOodle OodleFormat = new();

    public static byte[] CompressData(ReadOnlySpan<byte> data, CompressionType type, CompressionOptions options)
    {
        ICompressionFormat? compressor;
        switch (type)
        {
            case CompressionType.None:
                compressor = null;
                break;
            case CompressionType.Zstd:
                compressor = ZstdFormat;
                break;
            case CompressionType.Lz4:
                compressor = Lz4Format;
                break;
            case CompressionType.ZLib:
                compressor = ZLibFormat;
                break;
            case CompressionType.OodleKraken:
                compressor = OodleFormat;
                options = CompressionOptions.OodleKraken;
                break;
            case CompressionType.OodleSelkie:
                compressor = OodleFormat;
                options = CompressionOptions.OodleSelkie;
                break;
            case CompressionType.OodleLeviathan:
                compressor = OodleFormat;
                options = CompressionOptions.OodleLeviathan;
                break;
            case CompressionType.Obfuscated:
                throw new NotImplementedException("Writing Obfuscated CAS blocks is not supported yet.");
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Not a CAS compression type.");
        }

        byte flags = (byte)(options == CompressionOptions.ZstdWithDict ? 1 : 0);
        int maxBufferSize = ProfilesLibrary.MaxBufferSize;
        int blockCount = (data.Length + maxBufferSize - 1) / maxBufferSize;

        // Allocate a reusable buffer sized for the largest single block's compression bounds.
        byte[] compressBuffer = compressor is null || blockCount == 0
            ? [] // "= []" allocates nothing in memory
            : GC.AllocateUninitializedArray<byte>(
                compressor.GetCompressBounds(
                    Math.Min(maxBufferSize, data.Length), options));

        using MemoryStream stream = new();
        using (DataAccessor accessor = new(stream, true))
        {
            for (int i = 0; i < blockCount; i++)
            {
                ReadOnlySpan<byte> block = data.Slice(i * maxBufferSize,
                    Math.Min(maxBufferSize, data.Length - (i * maxBufferSize)));

                if (compressor is null)
                {
                    accessor.WriteUInt64(new BlockHeader(flags, block.Length, type, block.Length).ToPacked(),
                        Endian.Big);
                    accessor.Write(block);
                }
                else
                {
                    int compressedSize = compressor.Compress(block, compressBuffer, options);

                    accessor.WriteUInt64(new BlockHeader(flags, block.Length, type, compressedSize).ToPacked(),
                        Endian.Big);
                    accessor.Write(compressBuffer.AsSpan(0, compressedSize));
                }
            }
        }

        return stream.ToArray();
    }

    public static byte[] DecompressData(DataAccessor accessor, int originalSize)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        byte[] outBuffer = new byte[originalSize];
        int outPos = 0;
        while (accessor.Position < accessor.Length)
        {
            outPos += ReadBlock(accessor, outBuffer.AsSpan(outPos));
        }

        Debug.Assert(outPos == originalSize);

        return outBuffer;
    }

    /// <summary>
    ///     Decompresses patched CAS data by applying delta instructions to base data.
    /// </summary>
    /// <param name="deltaAccessor">The accessor containing delta instructions and new data blocks.</param>
    /// <param name="baseAccessor">
    ///     The accessor containing base data blocks, or <see langword="null" /> if no base data is
    ///     required.
    /// </param>
    /// <param name="originalSize">The expected uncompressed size in bytes.</param>
    /// <remarks>
    ///     Delta instructions specify whether to copy base blocks, patch base blocks with delta bytes,
    ///     insert new blocks from the delta stream, or skip base blocks.
    /// </remarks>
    public static byte[] DecompressData(DataAccessor deltaAccessor, DataAccessor? baseAccessor,
        int originalSize)
    {
        ArgumentNullException.ThrowIfNull(deltaAccessor);

        byte[] outBuffer = new byte[originalSize];
        int outPos = 0;
        while (deltaAccessor.Position < deltaAccessor.Length)
        {
            (int instructionType, int instructionSize) = ReadInstruction(deltaAccessor);

            switch (instructionType)
            {
                case 0:
                    {
                        Debug.Assert(baseAccessor is not null);
                        // Read base blocks
                        while (instructionSize-- > 0)
                        {
                            outPos += ReadBlock(baseAccessor, outBuffer.AsSpan(outPos));
                        }

                        break;
                    }
                case 1:
                    {
                        Debug.Assert(baseAccessor is not null);
                        // Apply block-level patches to a base block
                        byte[] toPatch = ReadBlock(baseAccessor);
                        int patchPos = 0;
                        while (instructionSize-- > 0)
                        {
                            ushort offset = deltaAccessor.ReadUInt16(Endian.Big);
                            ushort skipCount = deltaAccessor.ReadUInt16(Endian.Big);

                            // Copy base bytes up to the patch offset
                            CopyBase(toPatch, ref patchPos, offset - patchPos, outBuffer, ref outPos);

                            // Splice in replacement block from the delta stream
                            outPos += ReadBlock(deltaAccessor, outBuffer.AsSpan(outPos));

                            // Skip replaced base bytes
                            patchPos += skipCount;
                        }

                        // Copy remaining base block bytes
                        CopyBase(toPatch, ref patchPos, toPatch.Length - patchPos, outBuffer, ref outPos);

                        break;
                    }
                case 2:
                    {
                        Debug.Assert(baseAccessor is not null);
                        // Apply byte-level patches to a base block
                        ApplySmallFixes(deltaAccessor, baseAccessor, instructionSize, outBuffer, ref outPos);
                        break;
                    }
                case 3:
                    {
                        // Append new blocks from the delta stream
                        while (instructionSize-- > 0)
                        {
                            outPos += ReadBlock(deltaAccessor, outBuffer.AsSpan(outPos));
                        }

                        break;
                    }
                case 4:
                    {
                        Debug.Assert(baseAccessor is not null);

                        // Skip base blocks
                        while (instructionSize-- > 0)
                        {
                            SkipBlock(baseAccessor);
                        }

                        break;
                    }
                default:
                    throw new InvalidDataException(
                        string.Create(CultureInfo.InvariantCulture,
                            $"Unknown delta instruction type {instructionType}."));
            }
        }

        Debug.Assert(baseAccessor?.Position == baseAccessor?.Length);
        Debug.Assert(outPos == originalSize);

        return outBuffer;
    }

    /// <summary>
    ///     Decompresses patched CAS data starting from a mid-instruction block offset.
    /// </summary>
    /// <param name="deltaAccessor">The accessor containing delta instructions and new data blocks.</param>
    /// <param name="baseAccessor">
    ///     The accessor containing base data blocks, or <see langword="null" /> if no base data is
    ///     required.
    /// </param>
    /// <param name="originalSize">The expected uncompressed size in bytes.</param>
    /// <param name="inMidInstructionSize">
    ///     The remaining block count threshold for the first delta instruction. Blocks processed while
    ///     the remaining count is >= this value are skipped. Pass -1 to process all blocks.
    /// </param>
    public static byte[] DecompressData(DataAccessor deltaAccessor, DataAccessor? baseAccessor,
        int originalSize,
        int inMidInstructionSize)
    {
        ArgumentNullException.ThrowIfNull(deltaAccessor);

        byte[] outBuffer = new byte[originalSize];
        int outPos = 0;
        bool startBlock = true;
        int instructionSize;
        while (deltaAccessor.Position < deltaAccessor.Length)
        {
            (int instructionType, instructionSize) = ReadInstruction(deltaAccessor);

            switch (instructionType)
            {
                case 0:
                    {
                        // Read base blocks
                        Debug.Assert(baseAccessor is not null);
                        while (instructionSize-- > 0)
                        {
                            outPos += ReadOrSkipBlock(baseAccessor, outBuffer.AsSpan(outPos), IsSkipped());

                            if (IsFinished())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                case 1:
                    {
                        // Apply block-level patches to a base block
                        Debug.Assert(baseAccessor is not null);
                        byte[] toPatch = ReadBlock(baseAccessor);
                        int patchPos = 0;
                        while (instructionSize-- > 0)
                        {
                            ushort offset = deltaAccessor.ReadUInt16(Endian.Big);
                            ushort skipCount = deltaAccessor.ReadUInt16(Endian.Big);
                            bool isSkipped = IsSkipped();

                            // Copy base bytes up to the patch offset
                            int baseSize = offset - patchPos;
                            if (baseSize > 0)
                            {
                                if (!isSkipped)
                                {
                                    toPatch.AsSpan(patchPos, baseSize).CopyTo(outBuffer.AsSpan(outPos));
                                    outPos += baseSize;
                                }

                                patchPos += baseSize;
                            }

                            // Splice in replacement block from the delta stream
                            outPos += ReadOrSkipBlock(deltaAccessor, outBuffer.AsSpan(outPos), isSkipped);

                            // Skip replaced base bytes
                            patchPos += skipCount;

                            if (instructionSize != 0 && IsFinished())
                            {
                                return outBuffer;
                            }
                        }

                        // Copy remaining base block bytes
                        CopyBase(toPatch, ref patchPos, toPatch.Length - patchPos, outBuffer, ref outPos);

                        break;
                    }
                case 2:
                    {
                        // Apply byte-level patches to a base block
                        Debug.Assert(baseAccessor is not null);
                        ApplySmallFixes(deltaAccessor, baseAccessor, instructionSize, outBuffer, ref outPos);
                        break;
                    }
                case 3:
                    {
                        // Append new blocks from the delta stream
                        while (instructionSize-- > 0)
                        {
                            outPos += ReadOrSkipBlock(deltaAccessor, outBuffer.AsSpan(outPos), IsSkipped());

                            if (IsFinished())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                case 4:
                    {
                        // Skip base blocks
                        Debug.Assert(baseAccessor is not null);
                        while (instructionSize-- > 0)
                        {
                            SkipBlock(baseAccessor);

                            // Terminate early just in case both base and delta streams are finish. Probably not needed.
                            if (IsFinished())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                default:
                    throw new InvalidDataException(
                        string.Create(CultureInfo.InvariantCulture,
                            $"Unknown delta instruction type {instructionType}."));
            }

            startBlock = false;

            if (IsFinished())
            {
                return outBuffer;
            }
        }

        while (baseAccessor is not null && baseAccessor.Position < baseAccessor.Length)
        {
            outPos += ReadBlock(baseAccessor, outBuffer.AsSpan(outPos));
        }

        Debug.Assert(outPos == originalSize);

        return outBuffer;

        // Evaluated after instructionSize is decremented, representing the remaining blocks in the current instruction.
        bool IsSkipped()
        {
            return startBlock && inMidInstructionSize != -1 && instructionSize >= inMidInstructionSize;
        }

        bool IsFinished()
        {
            return (baseAccessor is null || baseAccessor.Position >= baseAccessor.Length) &&
                   deltaAccessor.Position >= deltaAccessor.Length;
        }
    }

    public static long GetUncompressedSize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        ulong packed = accessor.ReadUInt64(Endian.Big);

        if (packed == 0)
        {
            return 0;
        }

        BlockHeader header = BlockHeader.FromPacked(packed);

        accessor.Position += header.BufferSize;

        return header.DecompressedSize;
    }

    public static long GetOriginalSize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        long uncompressedSize = 0;

        while (accessor.Position < accessor.Length)
        {
            ulong packed = accessor.ReadUInt64(Endian.Big);

            if (packed == 0)
            {
                return 0;
            }

            BlockHeader header = BlockHeader.FromPacked(packed);

            accessor.Position += header.BufferSize;

            uncompressedSize += header.DecompressedSize;
        }

        return uncompressedSize;
    }

    public static long GetOriginalSize(DataAccessor deltaAccessor, DataAccessor? baseAccessor)
    {
        throw new NotImplementedException();
    }

    public static long GetOriginalSize(DataAccessor deltaAccessor, DataAccessor? baseAccessor,
        int inMidInstructionSize)
    {
        throw new NotImplementedException();
    }

    public static long GetCompressedSize(DataAccessor accessor, long uncompressedSize)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        long compressedSize = 0;
        while (accessor.Position < accessor.Length)
        {
            ulong packed = accessor.ReadUInt64(Endian.Big);

            if (packed == 0)
            {
                continue;
            }

            BlockHeader header = BlockHeader.FromPacked(packed);

            compressedSize += header.BufferSize + sizeof(ulong);

            accessor.Position += header.BufferSize;

            uncompressedSize -= header.DecompressedSize;
            if (uncompressedSize > 0)
            {
                continue;
            }

            Debug.Assert(uncompressedSize == 0);
            break;
        }

        return compressedSize;
    }

    /// <summary>
    ///     Reads a 32-bit delta instruction header.
    /// </summary>
    /// <remarks>
    ///     The high 4 bits specify the instruction type, and the lower 28 bits specify the block count or payload size.
    /// </remarks>
    private static (int Type, int Size) ReadInstruction(DataAccessor deltaAccessor)
    {
        uint packed = deltaAccessor.ReadUInt32(Endian.Big);
        return ((int)(packed >> 28), (int)(packed & 0x0FFFFFFF));
    }

    /// <summary>
    ///     Applies byte-level delta patches (instruction type 2) to a base block.
    /// </summary>
    /// <remarks>
    ///     Reads a base block and applies small replacements or insertions specified by offset,
    ///     skip count, and replacement bytes from the delta stream.
    /// </remarks>
    private static void ApplySmallFixes(DataAccessor deltaAccessor, DataAccessor baseAccessor,
        int inInstructionSize, byte[] outBuffer, ref int outPos)
    {
        int newBlockSize = deltaAccessor.ReadUInt16(Endian.Big) + 1;
        int startPos = outPos;

        long curPos = deltaAccessor.Position;

        // Read base block
        byte[] toPatch = ReadBlock(baseAccessor);
        int patchPos = 0;
        while (deltaAccessor.Position < curPos + inInstructionSize)
        {
            ushort offset = deltaAccessor.ReadUInt16(Endian.Big);
            byte skipCount = deltaAccessor.ReadByte();
            byte addCount = deltaAccessor.ReadByte();

            // Copy base bytes up to the patch offset
            CopyBase(toPatch, ref patchPos, offset - patchPos, outBuffer, ref outPos);

            // Skip replaced base bytes
            patchPos += skipCount;

            // Append replacement bytes from the delta stream
            deltaAccessor.ReadExactly(outBuffer.AsSpan(outPos, addCount));
            outPos += addCount;
        }

        // Copy remaining base block bytes
        CopyBase(toPatch, ref patchPos, newBlockSize - (outPos - startPos), outBuffer, ref outPos);

        Debug.Assert(patchPos == toPatch.Length, "The base block was not read completely.");
        Debug.Assert(outPos - startPos == newBlockSize, "The patched block has the wrong size.");
    }

    /// <summary>
    ///     Copies a span of bytes from the base block to the output buffer if <paramref name="size" /> is positive.
    /// </summary>
    private static void CopyBase(byte[] toPatch, ref int patchPos, int size, byte[] outBuffer, ref int outPos)
    {
        if (size <= 0)
        {
            return;
        }

        toPatch.AsSpan(patchPos, size).CopyTo(outBuffer.AsSpan(outPos));
        patchPos += size;
        outPos += size;
    }

    private static int ReadOrSkipBlock(DataAccessor accessor, Span<byte> destination, bool skip)
    {
        if (!skip)
        {
            return ReadBlock(accessor, destination);
        }

        SkipBlock(accessor);
        return 0;
    }

    /// <summary>
    ///     Reads and decompresses a single CAS block into <paramref name="destination" />.
    /// </summary>
    /// <returns>The number of decompressed bytes written, or 0 for an empty block.</returns>
    private static int ReadBlock(DataAccessor accessor, Span<byte> destination)
    {
        ulong packed = accessor.ReadUInt64(Endian.Big);

        if (packed == 0)
        {
            // Skip empty block, no point in parsing the header
            return 0;
        }

        BlockHeader header = BlockHeader.FromPacked(packed);
        ReadBlockData(accessor, header, destination[..header.DecompressedSize]);
        return header.DecompressedSize;
    }

    /// <summary>
    ///     Reads and decompresses an entire CAS block into a newly allocated byte array.
    /// </summary>
    private static byte[] ReadBlock(DataAccessor accessor)
    {
        BlockHeader header = BlockHeader.FromPacked(accessor.ReadUInt64(Endian.Big));

        byte[] outBuffer = GC.AllocateUninitializedArray<byte>(header.DecompressedSize);
        ReadBlockData(accessor, header, outBuffer);
        return outBuffer;
    }

    private static void SkipBlock(DataAccessor accessor)
    {
        ulong packed = accessor.ReadUInt64(Endian.Big);

        if (packed != 0)
        {
            accessor.Position += BlockHeader.FromPacked(packed).BufferSize;
        }
    }

    /// <summary>
    ///     Reads and decompresses CAS block payload data following its header.
    /// </summary>
    /// <param name="accessor">The accessor positioned at the block payload.</param>
    /// <param name="header">The parsed header describing the block.</param>
    /// <param name="destination">The destination span sized to the block decompressed size.</param>
    private static void ReadBlockData(DataAccessor accessor, BlockHeader header, Span<byte> destination)
    {
        if (header.IsUncompressed)
        {
            // Data is read right into the destination
            accessor.ReadExactly(destination);
            Deobfuscate(destination, header);
            return;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(header.BufferSize);
        try
        {
            // Memory rented from ArrayPool may be larger than requested.
            Span<byte> compressed = rented.AsSpan(0, header.BufferSize);
            accessor.ReadExactly(compressed);
            Deobfuscate(compressed, header);
            Decompress(compressed, header, destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static void Deobfuscate(Span<byte> data, BlockHeader header)
    {
        if (!header.Type.HasFlag(CompressionType.Obfuscated))
        {
            return;
        }

        // This probably only exists in FIFA 19, should be fine to check it here.
        // I doubt they will ever have 128 different compression types,
        // and currently they are at 25. (2023-12-08 by wannkunstbeikor)
        if (!ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19))
        {
            throw new NotImplementedException(
                "CompressionType.Obfuscated CAS data currently only supports FIFA 19.");
        }

        ReadOnlySpan<byte> key = KeyManager.GetKey("CasObfuscationKey");
        for (int i = 0; i < data.Length; i++)
        {
            data[i] ^= key[i % key.Length];
        }
    }

    private static void Decompress(ReadOnlySpan<byte> compressed, BlockHeader header, Span<byte> destination)
    {
        ICompressionFormat decompressor = (header.Type & ~CompressionType.Obfuscated) switch
        {
            CompressionType.ZLib => ZLibFormat,
            CompressionType.Zstd => ZstdFormat,
            CompressionType.Lz4 => Lz4Format,
            CompressionType.OodleKraken or CompressionType.OodleSelkie or CompressionType.OodleLeviathan => OodleFormat,
            _ => throw new NotImplementedException(
                string.Create(CultureInfo.InvariantCulture, $"Compression type: {header.Type}"))
        };

        // Oodle fails if the destination buffer is larger than the exact decompressed size.
        // Oodle decompression detects the compression type automatically from the payload.
        int written = decompressor.Decompress(compressed, destination,
            header.Flags != 0 ? CompressionOptions.ZstdWithDict : CompressionOptions.None);
        if (written != header.DecompressedSize)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture,
                    $"A {header.Type} block decompressed to {written} bytes instead of {header.DecompressedSize}."));
        }
    }

    /// <summary>
    ///     Represents the 8-byte big-endian header preceding each CAS block.
    /// </summary>
    private readonly record struct BlockHeader(byte Flags, int DecompressedSize, CompressionType Type, int BufferSize)
    {
        public bool IsUncompressed => (Type & ~CompressionType.Obfuscated) == CompressionType.None;

        public static BlockHeader FromPacked(ulong packed)
        {
            Debug.Assert(((packed >> 20) & 0xF) == 7, "Invalid CAS data");

            int decompressedSize = (int)((packed >> 32) & 0x00FFFFFF);
            CompressionType type = (CompressionType)(byte)(packed >> 24);
            bool isUncompressed = (type & ~CompressionType.Obfuscated) == CompressionType.None;

            return new BlockHeader((byte)(packed >> 56), decompressedSize, type,
                isUncompressed ? decompressedSize : (int)(packed & 0x000FFFFF));
        }

        public ulong ToPacked()
        {
            return ((ulong)Flags << 56)
                   | ((ulong)(uint)DecompressedSize << 32)
                   | ((ulong)(byte)Type << 24)
                   | (0x7UL << 20)
                   | (uint)BufferSize;
        }
    }
}