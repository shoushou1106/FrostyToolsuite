using Frosty.Sdk.IO;
using Frosty.Sdk.Managers.Infos.FileInfos;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Interfaces;

public interface IFileInfo
{
    bool IsDelta();

    bool IsComplete();

    bool FileExists();

    long GetOriginalSize();

    Block<byte> GetRawData();

    Block<byte> GetData(int inOriginalSize);

    protected void SerializeInternal(DataStream stream);

    static void Serialize(DataStream stream, IFileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(fileInfo);

        switch (fileInfo)
        {
            case CasFileInfo:
                stream.WriteByte(0);
                break;
            case KelvinFileInfo:
                stream.WriteByte(1);
                break;
            case NonCasFileInfo:
                stream.WriteByte(2);
                break;
            default:
                throw new NotImplementedException();
        }

        fileInfo.SerializeInternal(stream);
    }

    static IFileInfo Deserialize(DataStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte type = stream.ReadByte();

        return type switch
        {
            0 => CasFileInfo.DeserializeInternal(stream),
            1 => KelvinFileInfo.DeserializeInternal(stream),
            2 => NonCasFileInfo.DeserializeInternal(stream),
            _ => throw new InvalidDataException()
        };
    }
}