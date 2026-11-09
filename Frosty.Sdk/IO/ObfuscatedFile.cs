using System.Buffers.Binary;

using Microsoft.Extensions.Logging;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads files that can start with a Frostbite obfuscation header, like <c>toc</c>, <c>sb</c> and <c>cas.cat</c>.
/// </summary>
/// <remarks>
///     The header is 0x22C bytes and starts with <c>00 D1 CE</c> and a version byte.
///     Versions 0 and 1 XOR the data with a 257 byte key from the header, but only up to Frostbite 2014.4.11.
///     Version 3 leaves the data as is.
///     Mass Effect Andromeda also obfuscates the whole file including the header, and appends a trailer
///     that ends with a 32 byte magic string.
/// </remarks>
public static class ObfuscatedFile
{
    private const int HeaderSize = 0x22C;
    private const int KeyOffset = 0x128;
    private const int KeyLength = 0x101;

    // The size of the whole trailer as int32, then the magic
    private const int TrailerEndSize = sizeof(int) + 32;

    // For MEA
    private static ReadOnlySpan<byte> TrailerMagic => "@e!adnXd$^!rfOsrDyIrI!xVgHeA!6Vc"u8;

    /// <summary>
    ///     Reads a whole file and removes its obfuscation.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The data after the obfuscation header, or the whole file if it has no obfuscation header.</returns>
    /// <exception cref="IOException">The file is larger than an array can hold.</exception>
    /// <exception cref="InvalidDataException">The file has a trailer with an invalid size.</exception>
    /// <exception cref="NotImplementedException">The file has a trailer, but no obfuscation header.</exception>
    public static byte[] ReadAllBytes(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read);

        if (stream.Length > Array.MaxLength)
        {
            throw new IOException($"\"{path}\" is larger than {Array.MaxLength} bytes.");
        }

        int length = (int)stream.Length;

        byte[]? data = TryReadTrailerSize(stream, length, out int trailerSize)
            ? ReadWithTrailer(stream, length - trailerSize, path)
            : ReadWithHeader(stream, length);

        if (data is not null)
        {
            return data;
        }

        data = GC.AllocateUninitializedArray<byte>(length);
        stream.Position = 0;
        stream.ReadExactly(data);
        return data;
    }

    private static bool TryReadTrailerSize(FileStream stream, int length, out int trailerSize)
    {
        trailerSize = 0;
        if (length < TrailerEndSize)
        {
            return false;
        }

        Span<byte> trailerEnd = stackalloc byte[TrailerEndSize];
        stream.Position = length - TrailerEndSize;
        stream.ReadExactly(trailerEnd);
        stream.Position = 0;

        if (!trailerEnd[sizeof(int)..].SequenceEqual(TrailerMagic))
        {
            return false;
        }

        trailerSize = BinaryPrimitives.ReadInt32LittleEndian(trailerEnd);
        if (trailerSize < TrailerEndSize || trailerSize > length)
        {
            throw new InvalidDataException($"The obfuscation trailer size {trailerSize} is invalid.");
        }

        return true;
    }

    /// <returns>The data, or <see langword="null" /> if the file has no obfuscation header.</returns>
    private static byte[]? ReadWithHeader(FileStream stream, int length)
    {
        if (length < HeaderSize)
        {
            return null;
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        stream.ReadExactly(header);
        return IsHeader(header) ? ReadData(stream, header, length - HeaderSize) : null;
    }

    private static byte[] ReadData(FileStream stream, ReadOnlySpan<byte> header, int dataLength)
    {
        byte[] data = GC.AllocateUninitializedArray<byte>(dataLength);
        stream.ReadExactly(data);
        RemoveHeaderKey(header, data);
        return data;
    }

    /// <param name="stream">The file.</param>
    /// <param name="length">The length of the file without the trailer.</param>
    /// <param name="path">The path of the file for logging.</param>
    /// <exception cref="NotImplementedException">The data does not start with an obfuscation header.</exception>
    /// <remarks>
    ///     <see cref="IsHeader" /> also accepts version 0, which v1's <c>MEADeobfuscator</c> does not.
    ///     A file with the trailer is not expected to have it.
    /// </remarks>
    private static byte[] ReadWithTrailer(FileStream stream, int length, string path)
    {
        if (length < HeaderSize)
        {
            // Usually unreachable.
            // Decoding it needs the key from the trailer, see MEADeobfuscator in Frosty v1
            throw new NotImplementedException(
                $"\"{path}\" has the obfuscation trailer, but its data does not start with an obfuscation header.");
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        stream.ReadExactly(header);

        // v1 leaves the data as it is when the magic in the trailer is not 4
        if (IsHeader(header))
        {
            FrostyLogger.Logger.LogWarning(
                "\"{Path}\" has the obfuscation trailer, but its data is not obfuscated.", path);
            return ReadData(stream, header, length - HeaderSize);
        }

        // Frosty v1's MEADeobfuscator deobfuscate the whole trailer just to read the initial key from the trailer.
        // This is a shortcut,
        // because the first plain byte is the 0 from the header magic (00 D1 CE), so 0 itself is the initial XOR key.
        byte initialValue = header[0];
        byte key = initialValue;
        RemoveTrailerKey(header, 0, initialValue, ref key);
        if (!IsHeader(header))
        {
            // Usually unreachable.
            // Decoding it needs the key from the trailer, see MEADeobfuscator in Frosty v1
            throw new NotImplementedException(
                $"\"{path}\" has the obfuscation trailer, but its data does not start with an obfuscation header.");
        }

        byte[] data = GC.AllocateUninitializedArray<byte>(length - HeaderSize);
        stream.ReadExactly(data);
        RemoveTrailerKey(data, HeaderSize, initialValue, ref key);
        RemoveHeaderKey(header, data);
        return data;
    }
    
    private static bool IsHeader(ReadOnlySpan<byte> header)
    {
        return header[0] == 0x00 && header[1] == 0xD1 && header[2] == 0xCE && header[3] is 0x00 or 0x01 or 0x03;
    }

    private static void RemoveHeaderKey(ReadOnlySpan<byte> header, Span<byte> data)
    {
        if (header[3] == 0x03 || ProfilesLibrary.FrostbiteVersion > "2014.4.11")
        {
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            data[i] ^= (byte)(0x7B ^ header[KeyOffset + (i % KeyLength)]);
        }
    }

    /// <param name="bytes">The bytes to decode in place.</param>
    /// <param name="offset">The offset of <paramref name="bytes" /> in the file.</param>
    /// <param name="initialValue">The first byte of the file.</param>
    /// <param name="key">The key for the next byte, updated for the byte after <paramref name="bytes" />.</param>
    private static void RemoveTrailerKey(Span<byte> bytes, int offset, byte initialValue, ref byte key)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            // The cast keeps the low 8 bits, the same as subtracting the offset modulo 256
            byte nextKey = (byte)((initialValue ^ bytes[i]) - (offset + i));
            bytes[i] ^= key;
            key = nextKey;
        }
    }
}
