namespace Frosty.Sdk.IO;

/// <summary>
///     Writes a RIFF file chunk by chunk, then <see cref="Fixup" /> writes the size into the header.
/// </summary>
public sealed class RiffWriter(DataAccessor inAccessor)
{
    private long _curPos;

    public void WriteHeader(FourCc inFileFourCc)
    {
        _curPos = inAccessor.Position;

        FourCc riff = "RIFF";
        inAccessor.WriteUInt32(riff);

        // Fixup after all chunks are written
        inAccessor.WriteUInt32(0xdeadbeef);

        inAccessor.WriteUInt32(inFileFourCc);
    }

    public void WriteChunk(FourCc inFourCc, ReadOnlySpan<byte> inData)
    {
        inAccessor.WriteUInt32(inFourCc);
        inAccessor.WriteInt32(inData.Length);
        inAccessor.Write(inData);

        // Written instead of skipped, so the last chunk keeps its pad byte too
        if (inData.Length % 2 != 0)
        {
            inAccessor.WriteByte(0);
        }
    }

    public void Fixup()
    {
        inAccessor.Position = _curPos + sizeof(uint);
        inAccessor.WriteUInt32((uint)(inAccessor.Length - inAccessor.Position - 4));
        inAccessor.Position = inAccessor.Length;
    }
}
