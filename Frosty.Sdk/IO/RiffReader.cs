namespace Frosty.Sdk.IO;

/// <summary>
///     Reads a RIFF file chunk by chunk.
/// </summary>
/// <remarks>
///     Only the little-endian <c>RIFF</c> form is supported. Frosty 1.0.7 rejects <c>RIFX</c> too.
/// </remarks>
public sealed class RiffReader(DataAccessor inAccessor)
{
    public bool Eof { get; private set; }

    /// <exception cref="InvalidDataException">The data does not start with <c>RIFF</c>.</exception>
    public void ReadHeader(out FourCc fileFourCc, out uint size)
    {
        FourCc fourCc = inAccessor.ReadUInt32();

        if (fourCc != "RIFF")
        {
            throw new InvalidDataException($"Not a RIFF file, the magic is \"{fourCc}\".");
        }

        size = inAccessor.ReadUInt32();

        fileFourCc = inAccessor.ReadUInt32();
    }

    public void ReadNextChunk(ref uint size, Action<DataAccessor, FourCc, uint> processChunkFunc)
    {
        ArgumentNullException.ThrowIfNull(processChunkFunc);

        if (Eof)
        {
            throw new EndOfStreamException();
        }

        FourCc fourCc = inAccessor.ReadUInt32();

        uint subSize = inAccessor.ReadUInt32();
        uint paddedSize = subSize + (subSize % 2);

        long curPos = inAccessor.Position;

        size -= paddedSize;

        processChunkFunc(inAccessor, fourCc, subSize);

        inAccessor.Position = curPos + paddedSize;

        // Allows a missing pad byte after the last chunk
        if (inAccessor.Position >= inAccessor.Length)
        {
            Eof = true;
        }
    }
}
