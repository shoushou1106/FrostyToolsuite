using Frosty.Sdk.IO;

namespace Frosty.Sdk.Utils;

public sealed class RiffWriter(DataAccessor inAccessor)
{
    private long _curPos;

    public void WriteHeader(FourCc inFourCc, FourCc inFileFourCc)
    {
        _curPos = inAccessor.Position;
        if (inFourCc != "RIFF" && inFourCc != "RIFX")
        {
            throw new FormatException("Not a valid RIFF format.");
        }

        inAccessor.WriteUInt32(inFourCc);

        // Fixup after all chunks are written
        inAccessor.WriteUInt32(0xdeadbeef);

        inAccessor.WriteUInt32(inFileFourCc);
    }

    public void WriteChunk(FourCc inFourCc, Block<byte> inData)
    {
        ArgumentNullException.ThrowIfNull(inData);

        inAccessor.WriteUInt32(inFourCc);
        inAccessor.WriteInt32(inData.Size);
        inAccessor.Write(inData);
        inAccessor.Pad(2);
    }

    public void Fixup()
    {
        inAccessor.Position = _curPos + sizeof(uint);
        inAccessor.WriteUInt32((uint)(inAccessor.Length - inAccessor.Position - 4));
        inAccessor.Position = inAccessor.Length;
    }
}
