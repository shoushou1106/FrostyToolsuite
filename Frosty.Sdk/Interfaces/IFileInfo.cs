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

    protected void SerializeInternal(DataAccessor accessor);

    static void Serialize(DataAccessor accessor, IFileInfo fileInfo)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(fileInfo);

        switch (fileInfo)
        {
            case CasFileInfo:
                accessor.WriteByte(0);
                break;
            case KelvinFileInfo:
                accessor.WriteByte(1);
                break;
            case NonCasFileInfo:
                accessor.WriteByte(2);
                break;
            default:
                throw new NotImplementedException();
        }

        fileInfo.SerializeInternal(accessor);
    }

    static IFileInfo Deserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        byte type = accessor.ReadByte();

        return type switch
        {
            0 => CasFileInfo.DeserializeInternal(accessor),
            1 => KelvinFileInfo.DeserializeInternal(accessor),
            2 => NonCasFileInfo.DeserializeInternal(accessor),
            _ => throw new InvalidDataException()
        };
    }
}