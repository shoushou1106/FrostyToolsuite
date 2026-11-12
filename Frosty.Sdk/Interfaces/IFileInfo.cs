using System.Globalization;

using Frosty.Sdk.IO;
using Frosty.Sdk.Managers.Infos.FileInfos;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Interfaces;

/// <summary>
///     Where the data of an asset or a resource is stored in the game files, and how to read it.
/// </summary>
public interface IFileInfo
{
    /// <summary>
    ///     Gets whether the data is a patch (delta) that is applied to base data.
    /// </summary>
    bool IsDelta();

    /// <summary>
    ///     Gets whether the stored data is the whole resource, not only a part of it (logical offset is 0).
    /// </summary>
    bool IsComplete();

    /// <summary>
    ///     Gets whether the files that store the data exist.
    /// </summary>
    bool FileExists();

    /// <summary>
    ///     Gets the size of the data after decompression.
    /// </summary>
    long GetOriginalSize();

    /// <summary>
    ///     Reads the raw stored bytes without decompression.
    /// </summary>
    Block<byte> GetRawData();

    /// <summary>
    ///     Reads and decompresses the data.
    /// </summary>
    /// <param name="originalSize">The size of the data after decompression.</param>
    Block<byte> GetData(int originalSize);

    /// <summary>
    ///     Writes the fields of the implementation, after the type byte written by <see cref="Serialize" />.
    /// </summary>
    protected void SerializeInternal(DataAccessor accessor);

    /// <summary>
    ///     Writes a type byte and the file info for the asset cache.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="fileInfo" /> is not a known implementation.</exception>
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
                throw new ArgumentException($"{fileInfo.GetType().Name} cannot be serialized.", nameof(fileInfo));
        }

        fileInfo.SerializeInternal(accessor);
    }

    /// <summary>
    ///     Reads a file info written by <see cref="Serialize" />.
    /// </summary>
    /// <exception cref="InvalidDataException">Type byte unknown.</exception>
    static IFileInfo Deserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        byte type = accessor.ReadByte();

        return type switch
        {
            0 => CasFileInfo.DeserializeInternal(accessor),
            1 => KelvinFileInfo.DeserializeInternal(accessor),
            2 => NonCasFileInfo.DeserializeInternal(accessor),
            _ => throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Unknown file info type {type}."))
        };
    }
}