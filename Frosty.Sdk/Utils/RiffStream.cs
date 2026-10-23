using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

// CA1711: Identifiers should not have incorrect suffix
#pragma warning disable CA1711
public class RiffStream(DataStream inStream)
{
    private long _curPos;
    public bool Eof { get; private set; }

    public void ReadHeader(out FourCc fileFourCc, out uint size)
    {
        FourCc fourCc = inStream.ReadUInt32();

        if (fourCc != "RIFF" && fourCc != "RIFX")
        {
            throw new FormatException("Not a valid RIFF format.");
        }

        size = inStream.ReadUInt32();

        fileFourCc = inStream.ReadUInt32();
    }

    public void ReadNextChunk(ref uint size, Action<DataStream, FourCc, uint> processChunkFunc)
    {
        ArgumentNullException.ThrowIfNull(processChunkFunc);

        if (Eof)
        {
            throw new EndOfStreamException();
        }

        FourCc fourCc = inStream.ReadUInt32();

        uint subSize = inStream.ReadUInt32();
        uint paddedSize = (subSize + 1u) & ~1u;

        long curPos = inStream.Position;

        size -= paddedSize;

        processChunkFunc(inStream, fourCc, subSize);

        inStream.Position = curPos + paddedSize;

        if (inStream.Position == inStream.Length)
        {
            Eof = true;
        }
    }

    public void WriteHeader(FourCc inFourCc, FourCc inFileFourCc)
    {
        _curPos = inStream.Position;
        if (inFourCc != "RIFF" && inFourCc != "RIFX")
        {
            throw new FormatException("Not a valid RIFF format.");
        }

        inStream.WriteUInt32(inFourCc);

        // fixup after all chunks are written
        inStream.WriteUInt32(0xdeadbeef);

        inStream.WriteUInt32(inFileFourCc);
    }

    public void WriteChunk(FourCc inFourCc, Block<byte> inData)
    {
        ArgumentNullException.ThrowIfNull(inData);

        inStream.WriteUInt32(inFourCc);
        inStream.WriteInt32(inData.Size);
        inStream.Write(inData);
        inStream.Pad(2);
    }

    public void Fixup()
    {
        inStream.Position = _curPos + sizeof(uint);
        inStream.WriteUInt32((uint)(inStream.Length - inStream.Position - 4));
        inStream.Position = inStream.Length;
    }
}
#pragma warning restore CA1711