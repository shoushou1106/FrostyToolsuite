using Frosty.Sdk.Managers.CatResources;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads the entries of a <c>cas.cat</c> file.
/// </summary>
public sealed class CatReader : IDisposable
{
    private const string CatMagic = "NyanNyanNyanNyan";

    private readonly DataAccessor _accessor;
    private readonly bool _isNewFormat;

    public CatReader(string inFilename)
    {
        _isNewFormat = ProfilesLibrary.FrostbiteVersion > "2014.4.11";
        _accessor = BlockAccessor.FromFile(inFilename, _isNewFormat);

        string magic = _accessor.ReadFixedSizedString(16);
        if (magic != CatMagic)
        {
            return;
        }

        ResourceCount = (uint)(_accessor.Length - _accessor.Position) / 0x20;
        PatchCount = 0;
        EncryptedCount = 0;

        // 2013.2, 2014.1, 2014.4.11
        if (!_isNewFormat)
        {
            return;
        }

        ResourceCount = _accessor.ReadUInt32();
        PatchCount = _accessor.ReadUInt32();

        // 2015.4.6, 2019-PR5, 2016.4.7, 2016.4.4, 2018.0
        if (ProfilesLibrary.FrostbiteVersion >= "2015")
        {
            EncryptedCount = _accessor.ReadUInt32();
            _accessor.Position += 12;
        }

        // Some games have encryption disabled
        if ((ResourceCount * 0x24) + (PatchCount * 0x3C) + (EncryptedCount * 0x50)
            != _accessor.Length - _accessor.Position)
        {
            EncryptedCount = 0;
        }
    }

    public uint ResourceCount { get; }
    public uint PatchCount { get; }
    public uint EncryptedCount { get; }

    public void Dispose()
    {
        _accessor.Dispose();
    }

    public CatResourceEntry ReadResourceEntry()
    {
        CatResourceEntry entry = new()
        {
            Sha1 = _accessor.ReadSha1(), Offset = _accessor.ReadUInt32(), Size = _accessor.ReadUInt32()
        };

        if (_isNewFormat)
        {
            entry.LogicalOffset = _accessor.ReadUInt32();
        }

        entry.ArchiveIndex = _accessor.ReadInt32() & 0xFF;
        return entry;
    }

    public CatResourceEntry ReadEncryptedEntry()
    {
        CatResourceEntry entry = new()
        {
            Sha1 = _accessor.ReadSha1(),
            Offset = _accessor.ReadUInt32(),
            Size = _accessor.ReadUInt32(),
            LogicalOffset = _accessor.ReadUInt32(),
            ArchiveIndex = _accessor.ReadInt32() & 0xFF,
            OriginalSize = _accessor.ReadUInt32(),
            IsEncrypted = true,
            KeyId = _accessor.ReadFixedSizedString(8),
            Checksum = _accessor.ReadBytes(32)
        };

        return entry;
    }

    public CatPatchEntry ReadPatchEntry()
    {
        CatPatchEntry entry = new()
        {
            Sha1 = _accessor.ReadSha1(), BaseSha1 = _accessor.ReadSha1(), DeltaSha1 = _accessor.ReadSha1()
        };
        return entry;
    }
}