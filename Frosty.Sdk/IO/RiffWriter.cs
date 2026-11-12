namespace Frosty.Sdk.IO;

/// <summary>
///     Writes a RIFF file chunk by chunk, then <see cref="Fixup" /> writes the size into the header.
/// </summary>
public sealed class RiffWriter(DataAccessor accessor)
{
    private long _curPos;

    public void WriteHeader(FourCc fileFourCc)
    {
        _curPos = accessor.Position;

        FourCc riff = "RIFF";
        accessor.WriteUInt32(riff);

        // Fixup after all chunks are written
        accessor.WriteUInt32(0xdeadbeef);

        accessor.WriteUInt32(fileFourCc);
    }

    public void WriteChunk(FourCc fourCc, ReadOnlySpan<byte> data)
    {
        accessor.WriteUInt32(fourCc);
        accessor.WriteInt32(data.Length);
        accessor.Write(data);

        // Written instead of skipped, so the last chunk keeps its pad byte too
        if (data.Length % 2 != 0)
        {
            accessor.WriteByte(0);
        }
    }

    public void Fixup()
    {
        accessor.Position = _curPos + sizeof(uint);
        accessor.WriteUInt32((uint)(accessor.Length - accessor.Position - 4));
        accessor.Position = accessor.Length;
    }
}