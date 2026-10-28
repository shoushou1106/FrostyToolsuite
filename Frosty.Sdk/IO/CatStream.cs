using Frosty.Sdk.Managers.CatResources;

namespace Frosty.Sdk.IO;

/// <summary>
///     Reads the entries of a <c>cas.cat</c> file.
/// </summary>
public sealed class CatStream : IDisposable
{
    public uint ResourceCount { get; }
    public uint PatchCount { get; }
    public uint EncryptedCount { get; }

    private const string CatMagic = "NyanNyanNyanNyan";

    private readonly DataStream _stream;
    private readonly bool _isNewFormat;

    public CatStream(string inFilename)
    {
        _isNewFormat = ProfilesLibrary.FrostbiteVersion > "2014.4.11";
        _stream = BlockStream.FromFile(inFilename, _isNewFormat);

        string magic = _stream.ReadFixedSizedString(16);
        if (magic != CatMagic)
        {
            return;
        }

        ResourceCount = (uint)(_stream.Length - _stream.Position) / 0x20;
        PatchCount = 0;
        EncryptedCount = 0;

        // 2013.2, 2014.1, 2014.4.11
        if (!_isNewFormat)
        {
            return;
        }

        ResourceCount = _stream.ReadUInt32();
        PatchCount = _stream.ReadUInt32();

        // 2015.4.6, 2019-PR5, 2016.4.7, 2016.4.4, 2018.0
        if (ProfilesLibrary.FrostbiteVersion >= "2015")
        {
            EncryptedCount = _stream.ReadUInt32();
            _stream.Position += 12;
        }

        // Some games have encryption disabled
        if ((ResourceCount * 0x24) + (PatchCount * 0x3C) + (EncryptedCount * 0x50)
            != _stream.Length - _stream.Position)
        {
            EncryptedCount = 0;
        }
    }

    public CatResourceEntry ReadResourceEntry()
    {
        CatResourceEntry entry = new()
        {
            Sha1 = _stream.ReadSha1(),
            Offset = _stream.ReadUInt32(),
            Size = _stream.ReadUInt32()
        };

        if (_isNewFormat)
        {
            entry.LogicalOffset = _stream.ReadUInt32();
        }

        entry.ArchiveIndex = _stream.ReadInt32() & 0xFF;
        return entry;
    }

    public CatResourceEntry ReadEncryptedEntry()
    {
        CatResourceEntry entry = new()
        {
            Sha1 = _stream.ReadSha1(),
            Offset = _stream.ReadUInt32(),
            Size = _stream.ReadUInt32(),
            LogicalOffset = _stream.ReadUInt32(),
            ArchiveIndex = _stream.ReadInt32() & 0xFF,
            OriginalSize = _stream.ReadUInt32(),
            IsEncrypted = true,
            KeyId = _stream.ReadFixedSizedString(8),
            Checksum = _stream.ReadBytes(32)
        };

        return entry;
    }

    public CatPatchEntry ReadPatchEntry()
    {
        CatPatchEntry entry = new()
        {
            Sha1 = _stream.ReadSha1(),
            BaseSha1 = _stream.ReadSha1(),
            DeltaSha1 = _stream.ReadSha1()
        };
        return entry;
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}