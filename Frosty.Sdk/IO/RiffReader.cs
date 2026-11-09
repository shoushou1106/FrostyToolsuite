using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

public sealed class RiffReader(DataAccessor inAccessor)
{
    public bool Eof { get; private set; }

    public void ReadHeader(out FourCc fileFourCc, out uint size)
    {
        FourCc fourCc = inAccessor.ReadUInt32();

        if (fourCc != "RIFF" && fourCc != "RIFX")
        {
            throw new FormatException("Not a valid RIFF format.");
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
        uint paddedSize = (subSize + 1u) & ~1u;

        long curPos = inAccessor.Position;

        size -= paddedSize;

        processChunkFunc(inAccessor, fourCc, subSize);

        inAccessor.Position = curPos + paddedSize;

        if (inAccessor.Position == inAccessor.Length)
        {
            Eof = true;
        }
    }
}
