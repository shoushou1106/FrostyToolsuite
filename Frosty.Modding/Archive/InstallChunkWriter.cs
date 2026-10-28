using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Frosty.Sdk;
using Frosty.Sdk.IO;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Managers.CatResources;
using Frosty.Sdk.Managers.Infos;
using Frosty.Sdk.Utils;

namespace Frosty.Modding.Archive;

public class InstallChunkWriter : IDisposable
{
    private const int c_maxCasFileSize = 1073741824;

    private readonly InstallChunkInfo m_installChunk;
    private readonly uint m_installChunkIndex;
    private int m_casIndex;
    private readonly bool m_isPatch;
    private readonly string m_dir;
    private readonly Dictionary<Sha1, (CasFileIdentifier, uint, uint)> m_data = new();
    private DataAccessor? m_currentAccessor;

    public InstallChunkWriter(InstallChunkInfo inInstallChunk, string inGamePatchPath, string inModDataPath, bool inIsPatch)
    {
        m_installChunk = inInstallChunk;
        m_installChunkIndex = FileSystemManager.GetInstallChunkIndex(m_installChunk);
        m_isPatch = inIsPatch;

        // create mod dir
        m_dir = Path.Combine(inModDataPath, m_installChunk.InstallBundle);
        Directory.CreateDirectory(m_dir);

        // create links to already existing cas files in this install bundle
        string dir = Path.Combine(inGamePatchPath, m_installChunk.InstallBundle);
        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.cas"))
            {
                int index = int.Parse(Path.GetFileName(file).AsSpan()[4..][..^4]);
                m_casIndex = Math.Max(index, m_casIndex);
                File.CreateSymbolicLink(Path.Combine(m_dir, $"cas_{index:D2}.cas"), file);
            }
        }

        m_casIndex++;
    }

    public (CasFileIdentifier, uint, uint) WriteData(Sha1 inSha1, Block<byte> inData)
    {
        if (m_data.TryGetValue(inSha1, out (CasFileIdentifier, uint, uint) retVal))
        {
            return retVal;
        }

        DataAccessor accessor = GetCurrentWriter(inData.Size);

        if (ProfilesLibrary.FrostbiteVersion <= "2014.4.11")
        {
            // write faceoff header
            accessor.WriteUInt32(0xFACE0FF0, Endian.Big);
            accessor.WriteSha1(inSha1);
            accessor.WriteInt64(inData.Size);
        }

        retVal = (new CasFileIdentifier(m_isPatch, m_installChunkIndex, m_casIndex), (uint)accessor.Position,
            (uint)inData.Size);
        m_data.Add(inSha1, retVal);
        accessor.Write(inData);

        return retVal;
    }

    public (CasFileIdentifier, uint, uint) GetFileInfo(Sha1 inSha1)
    {
        return m_data[inSha1];
    }

    public void WriteCatalog()
    {
        Block<byte> catalog = new(16);
        if (!FileSystemManager.TryResolvePath(Path.Combine(m_installChunk.InstallBundle, "cas.cat"), out string? originalPath))
        {
            throw new Exception();
        }

        using (BlockAccessor accessor = new(catalog, true))
        {
            accessor.WriteFixedSizedString("NyanNyanNyanNyan", 16);
            bool isNewFormat = ProfilesLibrary.FrostbiteVersion > "2014.4.11";
            using (CatReader catReader = new(originalPath))
            {
                if (isNewFormat)
                {
                    accessor.WriteUInt32(catReader.ResourceCount + (uint)m_data.Count);
                    accessor.WriteUInt32(catReader.PatchCount);
                    if (ProfilesLibrary.FrostbiteVersion >= "2015")
                    {
                        accessor.WriteUInt32(catReader.EncryptedCount);
                        accessor.Position += 12;
                    }
                }

                for (int i = 0; i < catReader.ResourceCount; i++)
                {
                    CatResourceEntry entry = catReader.ReadResourceEntry();
                    accessor.WriteSha1(entry.Sha1);
                    accessor.WriteUInt32(entry.Offset);
                    accessor.WriteUInt32(entry.Size);

                    if (isNewFormat)
                    {
                        accessor.WriteUInt32(entry.LogicalOffset);
                    }

                    accessor.WriteInt32(entry.ArchiveIndex);
                }

                foreach (KeyValuePair<Sha1,(CasFileIdentifier Identifier, uint Offset, uint Size)> data in m_data)
                {
                    accessor.WriteSha1(data.Key);
                    accessor.WriteUInt32(data.Value.Offset);
                    accessor.WriteUInt32(data.Value.Size);

                    if (isNewFormat)
                    {
                        accessor.WriteUInt32(0);
                    }

                    accessor.WriteInt32(data.Value.Identifier.CasIndex);
                }

                for (int i = 0; i < catReader.EncryptedCount; i++)
                {
                    CatResourceEntry entry = catReader.ReadEncryptedEntry();
                    accessor.WriteSha1(entry.Sha1);
                    accessor.WriteUInt32(entry.Offset);
                    accessor.WriteUInt32(entry.Size);
                    accessor.WriteUInt32(entry.LogicalOffset);
                    accessor.WriteInt32(entry.ArchiveIndex | (1 << 8));
                    accessor.WriteUInt32(entry.OriginalSize);
                    accessor.WriteFixedSizedString(entry.KeyId, 8);
                    accessor.Write(entry.Checksum);
                }

                for (int i = 0; i < catReader.PatchCount; i++)
                {
                    CatPatchEntry entry = catReader.ReadPatchEntry();
                    accessor.WriteSha1(entry.Sha1);
                    accessor.WriteSha1(entry.BaseSha1);
                    accessor.WriteSha1(entry.DeltaSha1);
                }
            }
        }

        string fileName = Path.Combine(m_dir, "cas.cat");
        using (FileStream stream = new(fileName, FileMode.CreateNew))
        {
            if (ProfilesLibrary.FrostbiteVersion > "2014.4.11")
            {
                ObfuscationHeader.Write(stream);
            }
            stream.Write(catalog);
        }
    }

    public List<CasFileIdentifier> GetFiles()
    {
        List<CasFileIdentifier> retVal = new(m_data.Values.Count);
        foreach ((CasFileIdentifier, uint, uint) value in m_data.Values)
        {
            retVal.Add(value.Item1);
        }
        return retVal;
    }

    private DataAccessor GetCurrentWriter(int size)
    {
        FileInfo currentFile = new(Path.Combine(m_dir, $"cas_{m_casIndex:D2}.cas"));

        if (currentFile.Exists && currentFile.Length + size > c_maxCasFileSize)
        {
            m_casIndex++;
            currentFile = new FileInfo(Path.Combine(m_dir, $"cas_{m_casIndex:D2}.cas"));
            Debug.Assert(!currentFile.Exists, "Trying to create new cas archive, even tho it already exists");
            m_currentAccessor?.Dispose();
            m_currentAccessor = new DataAccessor(currentFile.Create());
        }
        else if (m_currentAccessor is null)
        {
            Debug.Assert(!currentFile.Exists, "First cas archive used should not exist");
            m_currentAccessor = new DataAccessor(currentFile.Create());
            m_currentAccessor.Seek(0, SeekOrigin.End);
        }

        return m_currentAccessor;
    }

    public void Dispose()
    {
        m_currentAccessor?.Dispose();
    }
}