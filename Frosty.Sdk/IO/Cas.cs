using System.Diagnostics;

using Frosty.Sdk.Exceptions;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO.Compression;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Profiles;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.IO;

public static class Cas
{
    private static readonly CompressionZLib ZLibFormat = new();
    private static readonly CompressionZstd ZstdFormat = new();
    private static readonly CompressionLz4 Lz4Format = new();
    private static readonly CompressionOodle OodleFormat = new();

    public static Block<byte> CompressData(Block<byte> inData, CompressionType inType, CompressionOptions inOptions)
    {
        ArgumentNullException.ThrowIfNull(inData);

        int blockCount = (inData.Size + ProfilesLibrary.MaxBufferSize - 1) / ProfilesLibrary.MaxBufferSize;

        Block<byte> outData = new(0);
        using (BlockAccessor accessor = new(outData, true))
        {
            for (int i = 0; i < blockCount; i++)
            {
                int decompressedSize = Math.Min(ProfilesLibrary.MaxBufferSize, inData.Size);
                ReadOnlySpan<byte> data = inData.ToReadOnlySpan(0, decompressedSize);

                ICompressionFormat? compressor = null;
                switch (inType)
                {
                    case CompressionType.None:
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
                        inOptions = CompressionOptions.OodleKraken;
                        break;
                    case CompressionType.OodleSelkie:
                        compressor = OodleFormat;
                        inOptions = CompressionOptions.OodleSelkie;
                        break;
                    case CompressionType.OodleLeviathan:
                        compressor = OodleFormat;
                        inOptions = CompressionOptions.OodleLeviathan;
                        break;
                    case CompressionType.Obfuscated:
                        throw new NotImplementedException("Writing Obfuscated CAS blocks is not supported yet.");
                    default:
                        throw new UnknownValueException<CompressionType>("CompressionType", inType);
                }

                // Do not Dispose() something twice just to save few lines
                byte flags = (byte)(inOptions == CompressionOptions.ZstdWithDict ? 1 : 0);
                if (compressor is null)
                {
                    accessor.WriteUInt64(
                        new BlockHeader(flags, decompressedSize, inType, decompressedSize).ToPacked(), Endian.Big);
                    accessor.Write(data);
                }
                else
                {
                    using Block<byte> compBuffer =
                        Block<byte>.CreateUninitialized(compressor.GetCompressBounds(decompressedSize, inOptions));
                    int compressedSize = compressor.Compress(data, compBuffer, inOptions);

                    accessor.WriteUInt64(
                        new BlockHeader(flags, decompressedSize, inType, compressedSize).ToPacked(), Endian.Big);
                    accessor.Write(compBuffer.ToSpan(0, compressedSize));
                }

                inData.Shift(decompressedSize);
            }
        }

        inData.ResetShift();

        return outData;
    }

    public static Block<byte> DecompressData(DataAccessor inAccessor, int inOriginalSize)
    {
        ArgumentNullException.ThrowIfNull(inAccessor);

        Block<byte> outBuffer = new(inOriginalSize);
        while (inAccessor.Position < inAccessor.Length)
        {
            ReadBlock(inAccessor, outBuffer);
        }

        Debug.Assert(outBuffer.Size == 0);

        outBuffer.ResetShift();

        return outBuffer;
    }

    public static Block<byte> DecompressData(DataAccessor inDeltaAccessor, DataAccessor? inBaseAccessor,
        int inOriginalSize)
    {
        ArgumentNullException.ThrowIfNull(inDeltaAccessor);

        Block<byte> outBuffer = new(inOriginalSize);
        while (inDeltaAccessor.Position < inDeltaAccessor.Length)
        {
            uint packed = inDeltaAccessor.ReadUInt32(Endian.Big);
            int instructionType = (int)(packed & 0xF0000000) >> 28;
            int instructionSize = (int)(packed & 0x0FFFFFFF);

            switch (instructionType)
            {
                case 0:
                    {
                        Debug.Assert(inBaseAccessor is not null);
                        // Read base blocks
                        while (instructionSize-- > 0)
                        {
                            ReadBlock(inBaseAccessor, outBuffer);
                        }

                        break;
                    }
                case 1:
                    {
                        Debug.Assert(inBaseAccessor is not null);
                        // Make large fixes in base block
                        using Block<byte> toPatch = ReadBlock(inBaseAccessor);
                        while (instructionSize-- > 0)
                        {
                            ushort offset = inDeltaAccessor.ReadUInt16(Endian.Big);
                            ushort skipCount = inDeltaAccessor.ReadUInt16(Endian.Big);

                            // Use base
                            int baseSize = offset - toPatch.ShiftAmount;
                            if (baseSize > 0)
                            {
                                toPatch.CopyTo(outBuffer, baseSize);
                                toPatch.Shift(baseSize);
                                outBuffer.Shift(baseSize);
                            }

                            // Use delta
                            ReadBlock(inDeltaAccessor, outBuffer);

                            // Skip base
                            toPatch.Shift(skipCount);
                        }

                        // Fill rest with base block
                        if (toPatch.Size > 0)
                        {
                            toPatch.CopyTo(outBuffer, toPatch.Size);
                            outBuffer.Shift(toPatch.Size);
                            toPatch.Shift(toPatch.Size);
                        }

                        break;
                    }
                case 2:
                    {
                        Debug.Assert(inBaseAccessor is not null);

                        // Make small fixes in base block
                        int newBlockSize = inDeltaAccessor.ReadUInt16(Endian.Big) + 1;
                        int currentOffset = outBuffer.ShiftAmount;

                        long curPos = inDeltaAccessor.Position;

                        // Read base block
                        using (Block<byte> toPatch = ReadBlock(inBaseAccessor))
                        {
                            while (inDeltaAccessor.Position < curPos + instructionSize)
                            {
                                ushort offset = inDeltaAccessor.ReadUInt16(Endian.Big);
                                byte skipCount = inDeltaAccessor.ReadByte();
                                byte addCount = inDeltaAccessor.ReadByte();

                                // Use base
                                int baseSize = offset - toPatch.ShiftAmount;
                                if (baseSize > 0)
                                {
                                    toPatch.CopyTo(outBuffer, baseSize);
                                    toPatch.Shift(baseSize);
                                    outBuffer.Shift(baseSize);
                                }

                                // Skip base
                                toPatch.Shift(skipCount);

                                // Add delta
                                inDeltaAccessor.ReadExactly(outBuffer.ToSpan(0, addCount));
                                outBuffer.Shift(addCount);
                            }

                            // Fill rest with base block
                            int restSize = newBlockSize - (outBuffer.ShiftAmount - currentOffset);
                            if (restSize > 0)
                            {
                                toPatch.CopyTo(outBuffer, restSize);
                                toPatch.Shift(restSize);
                                outBuffer.Shift(restSize);
                            }

                            Debug.Assert(toPatch.Size == 0, "Didnt read base block completely");
                        }

                        Debug.Assert(outBuffer.ShiftAmount - currentOffset == newBlockSize, "Fuck");

                        break;
                    }
                case 3:
                    {
                        // Read delta blocks
                        while (instructionSize-- > 0)
                        {
                            ReadBlock(inDeltaAccessor, outBuffer);
                        }

                        break;
                    }
                case 4:
                    {
                        Debug.Assert(inBaseAccessor is not null);

                        // Skip base blocks
                        while (instructionSize-- > 0)
                        {
                            ReadBlock(inBaseAccessor, null);
                        }

                        break;
                    }
                default:
                    throw new InvalidDataException("block type");
            }
        }

        Debug.Assert(inBaseAccessor?.Position == inBaseAccessor?.Length);
        Debug.Assert(outBuffer.Size == 0);

        outBuffer.ResetShift();

        return outBuffer;
    }

    public static Block<byte> DecompressData(DataAccessor inDeltaAccessor, DataAccessor? inBaseAccessor,
        int inOriginalSize,
        int inMidInstructionSize)
    {
        ArgumentNullException.ThrowIfNull(inDeltaAccessor);

        // outBuffer is returned to the caller
        // CA2000: Dispose objects before losing scope
#pragma warning disable CA2000
        Block<byte> outBuffer = new(inOriginalSize);
#pragma warning restore CA2000
        bool startBlock = true;
        while (inDeltaAccessor.Position < inDeltaAccessor.Length)
        {
            uint packed = inDeltaAccessor.ReadUInt32(Endian.Big);
            int instructionType = (int)(packed & 0xF0000000) >> 28;
            int instructionSize = (int)(packed & 0x0FFFFFFF);

            switch (instructionType)
            {
                case 0:
                    {
                        // Read base blocks
                        Debug.Assert(inBaseAccessor is not null);
                        while (instructionSize-- > 0)
                        {
                            if (startBlock && inMidInstructionSize != -1 && instructionSize >= inMidInstructionSize)
                            {
                                ReadBlock(inBaseAccessor, null);
                            }
                            else
                            {
                                ReadBlock(inBaseAccessor, outBuffer);
                            }

                            if (TryFinish())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                case 1:
                    {
                        // Make large fixes in base block
                        Debug.Assert(inBaseAccessor is not null);
                        using Block<byte> toPatch = ReadBlock(inBaseAccessor);
                        while (instructionSize-- > 0)
                        {
                            ushort offset = inDeltaAccessor.ReadUInt16(Endian.Big);
                            ushort skipCount = inDeltaAccessor.ReadUInt16(Endian.Big);

                            // Use base
                            int baseSize = offset - toPatch.ShiftAmount;
                            if (baseSize > 0)
                            {
                                if (!startBlock || inMidInstructionSize == -1 || instructionSize < inMidInstructionSize)
                                {
                                    toPatch.CopyTo(outBuffer, baseSize);
                                    outBuffer.Shift(baseSize);
                                }

                                toPatch.Shift(baseSize);
                            }

                            // Use delta
                            if (startBlock && inMidInstructionSize != -1 && instructionSize >= inMidInstructionSize)
                            {
                                ReadBlock(inDeltaAccessor, null);
                            }
                            else
                            {
                                ReadBlock(inDeltaAccessor, outBuffer);
                            }

                            // Skip base
                            toPatch.Shift(skipCount);

                            if (instructionSize != 0 && TryFinish())
                            {
                                return outBuffer;
                            }
                        }

                        // fill rest with base block
                        if (toPatch.Size > 0)
                        {
                            toPatch.CopyTo(outBuffer, toPatch.Size);
                            outBuffer.Shift(toPatch.Size);
                            toPatch.Shift(toPatch.Size);
                        }

                        break;
                    }
                case 2:
                    {
                        // Make small fixes in base block
                        Debug.Assert(inBaseAccessor is not null);

                        int newBlockSize = inDeltaAccessor.ReadUInt16(Endian.Big) + 1;
                        int currentOffset = outBuffer.ShiftAmount;

                        long curPos = inDeltaAccessor.Position;

                        // Read base block
                        using (Block<byte> toPatch = ReadBlock(inBaseAccessor))
                        {
                            while (inDeltaAccessor.Position < curPos + instructionSize)
                            {
                                ushort offset = inDeltaAccessor.ReadUInt16(Endian.Big);
                                byte skipCount = inDeltaAccessor.ReadByte();
                                byte addCount = inDeltaAccessor.ReadByte();

                                // Use base
                                int baseSize = offset - toPatch.ShiftAmount;
                                if (baseSize > 0)
                                {
                                    toPatch.CopyTo(outBuffer, baseSize);
                                    toPatch.Shift(baseSize);
                                    outBuffer.Shift(baseSize);
                                }

                                // Skip base
                                toPatch.Shift(skipCount);

                                // Add delta
                                inDeltaAccessor.ReadExactly(outBuffer.ToSpan(0, addCount));
                                outBuffer.Shift(addCount);
                            }

                            // Fill rest with base block
                            int restSize = newBlockSize - (outBuffer.ShiftAmount - currentOffset);
                            if (restSize > 0)
                            {
                                toPatch.CopyTo(outBuffer, restSize);
                                toPatch.Shift(restSize);
                                outBuffer.Shift(restSize);
                            }

                            Debug.Assert(toPatch.Size == 0, "Didnt read base block completely");
                        }

                        Debug.Assert(outBuffer.ShiftAmount - currentOffset == newBlockSize, "Fuck");

                        break;
                    }
                case 3:
                    {
                        // Read delta blocks
                        while (instructionSize-- > 0)
                        {
                            if (startBlock && inMidInstructionSize != -1 && instructionSize >= inMidInstructionSize)
                            {
                                ReadBlock(inDeltaAccessor, null);
                            }
                            else
                            {
                                ReadBlock(inDeltaAccessor, outBuffer);
                            }

                            if (TryFinish())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                case 4:
                    {
                        // Skip base blocks
                        Debug.Assert(inBaseAccessor is not null);
                        while (instructionSize-- > 0)
                        {
                            ReadBlock(inBaseAccessor, null);

                            // Probably not needed, add it just in case
                            if (TryFinish())
                            {
                                return outBuffer;
                            }
                        }

                        break;
                    }
                default:
                    throw new InvalidDataException("block type");
            }

            startBlock = false;

            if (TryFinish())
            {
                return outBuffer;
            }
        }

        while ((inBaseAccessor?.Position ?? 0) < (inBaseAccessor?.Length ?? 0))
        {
            ReadBlock(inBaseAccessor!, outBuffer);
        }

        Debug.Assert(outBuffer.Size == 0);

        outBuffer.ResetShift();

        return outBuffer;

        bool TryFinish()
        {
            if ((inBaseAccessor?.Position ?? 0) < (inBaseAccessor?.Length ?? 0) ||
                inDeltaAccessor.Position < inDeltaAccessor.Length)
            {
                return false;
            }

            Debug.Assert(inBaseAccessor?.Position == inBaseAccessor?.Length);
            Debug.Assert(inDeltaAccessor.Position == inDeltaAccessor.Length);

            outBuffer.ResetShift();
            return true;
        }
    }

    public static long GetUncompressedSize(DataAccessor inAccessor)
    {
        ArgumentNullException.ThrowIfNull(inAccessor);

        ulong packed = inAccessor.ReadUInt64(Endian.Big);

        if (packed == 0)
        {
            return 0;
        }

        BlockHeader header = BlockHeader.FromPacked(packed);

        inAccessor.Position += header.BufferSize;

        return header.DecompressedSize;
    }

    public static long GetOriginalSize(DataAccessor inAccessor)
    {
        ArgumentNullException.ThrowIfNull(inAccessor);

        long uncompressedSize = 0;

        while (inAccessor.Position < inAccessor.Length)
        {
            ulong packed = inAccessor.ReadUInt64(Endian.Big);

            if (packed == 0)
            {
                return 0;
            }

            BlockHeader header = BlockHeader.FromPacked(packed);

            inAccessor.Position += header.BufferSize;

            uncompressedSize += header.DecompressedSize;
        }

        return uncompressedSize;
    }

    public static long GetOriginalSize(DataAccessor inDeltaAccessor, DataAccessor? inBaseAccessor)
    {
        throw new NotImplementedException();
    }

    public static long GetOriginalSize(DataAccessor inDeltaAccessor, DataAccessor? inBaseAccessor,
        int inMidInstructionSize)
    {
        throw new NotImplementedException();
    }

    public static long GetCompressedSize(DataAccessor inAccessor, long inUncompressedSize)
    {
        ArgumentNullException.ThrowIfNull(inAccessor);

        long compressedSize = 0;
        while (inAccessor.Position < inAccessor.Length)
        {
            ulong packed = inAccessor.ReadUInt64(Endian.Big);

            if (packed == 0)
            {
                continue;
            }

            BlockHeader header = BlockHeader.FromPacked(packed);

            compressedSize += header.BufferSize + sizeof(ulong);

            inAccessor.Position += header.BufferSize;

            inUncompressedSize -= header.DecompressedSize;
            if (inUncompressedSize > 0)
            {
                continue;
            }

            Debug.Assert(inUncompressedSize == 0);
            break;
        }

        return compressedSize;
    }

    private static void ReadBlock(DataAccessor inAccessor, Block<byte>? outBuffer)
    {
        ulong packed = inAccessor.ReadUInt64(Endian.Big);

        if (packed == 0)
        {
            // Skip empty block, no point in parsing the header
            return;
        }

        BlockHeader header = BlockHeader.FromPacked(packed);

        Block<byte> compressedBuffer = header.IsUncompressed && outBuffer is not null
            ? new Block<byte>(outBuffer, header.BufferSize)
            : Block<byte>.CreateUninitialized(header.BufferSize);

        inAccessor.ReadExactly(compressedBuffer);
        if (outBuffer is not null)
        {
            Decompress(compressedBuffer, header, outBuffer);
        }

        compressedBuffer.Dispose();
        outBuffer?.Shift(header.DecompressedSize);
    }

    private static Block<byte> ReadBlock(DataAccessor inAccessor)
    {
        BlockHeader header = BlockHeader.FromPacked(inAccessor.ReadUInt64(Endian.Big));

        Block<byte> outBuffer = Block<byte>.CreateUninitialized(header.DecompressedSize);
        Block<byte> compressedBuffer =
            header.IsUncompressed ? outBuffer : Block<byte>.CreateUninitialized(header.BufferSize);

        inAccessor.ReadExactly(compressedBuffer);
        Decompress(compressedBuffer, header, outBuffer);

        // Dispose() compressed buffer if there is one
        if (!header.IsUncompressed)
        {
            compressedBuffer.Dispose();
        }

        return outBuffer;
    }

    private static void Decompress(Block<byte> inCompressedBuffer, BlockHeader inHeader, Block<byte> outBuffer)
    {
        Span<byte> compressed = inCompressedBuffer;
        if (inHeader.Type.HasFlag(CompressionType.Obfuscated))
        {
            // This probably only exists in FIFA 19, should be fine to check it here.
            // I doubt they will ever have 128 different compression types,
            // and currently they are at 25. (2023-12-08 by wannkunstbeikor)
            if (!ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19))
            {
                throw new NotImplementedException(
                    "CompressionType.Obfuscated CAS data is only supported for FIFA 19 for now.");
            }

            ReadOnlySpan<byte> key = KeyManager.GetKey("CasObfuscationKey");
            for (int i = 0; i < compressed.Length; i++)
            {
                compressed[i] ^= key[i & 0x3FFF];
            }
        }

        ICompressionFormat? decompressor = (inHeader.Type & ~CompressionType.Obfuscated) switch
        {
            // Data already read into the outBuffer, nothing to do for CompressionType.None
            CompressionType.None => null,
            CompressionType.ZLib => ZLibFormat,
            CompressionType.Zstd => ZstdFormat,
            CompressionType.Lz4 => Lz4Format,
            CompressionType.OodleKraken or CompressionType.OodleSelkie or CompressionType.OodleLeviathan => OodleFormat,
            _ => throw new NotImplementedException($"Compression type: {inHeader.Type}")
        };

        if (decompressor is null)
        {
            return;
        }

        // Oodle fails when buffer is bigger than the original size
        // Oodle decompress does not need specific compression type
        int written = decompressor.Decompress(compressed, outBuffer.ToSpan(0, inHeader.DecompressedSize),
            inHeader.Flags != 0 ? CompressionOptions.ZstdWithDict : CompressionOptions.None);
        if (written != inHeader.DecompressedSize)
        {
            throw new InvalidDataException(
                $"A {decompressor.Identifier} block decompressed to {written} bytes instead of {inHeader.DecompressedSize}.");
        }
    }

    /// <summary>
    ///     The 8-byte big endian header in front of every cas block.
    /// </summary>
    private readonly record struct BlockHeader(byte Flags, int DecompressedSize, CompressionType Type, int BufferSize)
    {
        public bool IsUncompressed => (Type & ~CompressionType.Obfuscated) == CompressionType.None;

        public static BlockHeader FromPacked(ulong inPacked)
        {
            Debug.Assert(((inPacked >> 20) & 0xF) == 7, "Invalid CAS data");

            int decompressedSize = (int)((inPacked >> 32) & 0x00FFFFFF);
            CompressionType type = (CompressionType)(byte)(inPacked >> 24);
            bool isUncompressed = (type & ~CompressionType.Obfuscated) == CompressionType.None;

            return new BlockHeader((byte)(inPacked >> 56), decompressedSize, type,
                isUncompressed ? decompressedSize : (int)(inPacked & 0x000FFFFF));
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