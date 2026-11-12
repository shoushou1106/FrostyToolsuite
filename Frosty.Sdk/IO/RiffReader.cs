using System.Globalization;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads a RIFF file chunk by chunk.
/// </summary>
/// <remarks>
///     Only the little-endian <c>RIFF</c> form is supported. Frosty 1.0.7 rejects <c>RIFX</c> too.
/// </remarks>
public sealed class RiffReader(DataAccessor accessor)
{
    public bool Eof { get; private set; }

    /// <exception cref="InvalidDataException">The data does not start with <c>RIFF</c>.</exception>
    public void ReadHeader(out FourCc fileFourCc, out uint size)
    {
        FourCc fourCc = accessor.ReadUInt32();

        if (fourCc != "RIFF")
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Not a RIFF file, the magic is \"{fourCc}\"."));
        }

        size = accessor.ReadUInt32();

        fileFourCc = accessor.ReadUInt32();
    }

    public void ReadNextChunk(ref uint size, Action<DataAccessor, FourCc, uint> processChunkFunc)
    {
        ArgumentNullException.ThrowIfNull(processChunkFunc);

        if (Eof)
        {
            throw new EndOfStreamException();
        }

        FourCc fourCc = accessor.ReadUInt32();

        uint subSize = accessor.ReadUInt32();
        uint paddedSize = subSize + (subSize % 2);

        long curPos = accessor.Position;

        size -= paddedSize;

        processChunkFunc(accessor, fourCc, subSize);

        accessor.Position = curPos + paddedSize;

        // Allows a missing pad byte after the last chunk
        if (accessor.Position >= accessor.Length)
        {
            Eof = true;
        }
    }
}