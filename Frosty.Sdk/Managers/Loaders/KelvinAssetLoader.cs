using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Managers.Infos;
using Frosty.Sdk.Managers.Infos.FileInfos;
using Frosty.Sdk.Utils;
using Microsoft.Extensions.Logging;

namespace Frosty.Sdk.Managers.Loaders;

public class KelvinAssetLoader : IAssetLoader
{
    private readonly struct FileIdentifier
    {
        public readonly int FileIndex;
        public readonly uint Offset;
        public readonly uint Size;

        public FileIdentifier(int inFileIndex, uint inOffset, uint inSize)
        {
            FileIndex = inFileIndex;
            Offset = inOffset;
            Size = inSize;
        }
    }

    public void Load()
    {
        foreach (SuperBundleInfo sbInfo in FileSystemManager.EnumerateSuperBundles())
        {
            foreach (SuperBundleInstallChunk sbIc in sbInfo.InstallChunks)
            {
                bool found = false;
                foreach (FileSystemSource source in FileSystemManager.Sources)
                {
                    switch (LoadSuperBundle(source, sbIc))
                    {
                        case Code.Continue:
                            found = true;
                            continue;
                        case Code.NotFound:
                            continue;
                        case Code.Stop:
                            found = true;
                            break;
                    }

                    break;
                }

                if (!found)
                {
                    FrostyLogger.Logger.LogWarning($"Couldn't find SuperBundle \"{sbIc.Name}\"");
                }
            }
        }
    }

    private Code LoadSuperBundle(FileSystemSource inSource, SuperBundleInstallChunk inSbIc)
    {
        if (!inSource.TryResolvePath($"{inSbIc.Name}.toc", out string? path))
        {
            return Code.NotFound;
        }

        using (BlockAccessor accessor = BlockAccessor.FromFile(path, true))
        {
            uint magic = accessor.ReadUInt32();
            uint bundlesOffset = accessor.ReadUInt32();
            uint chunksOffset = accessor.ReadUInt32();

            if (magic == 0xC3E5D5C3)
            {
                accessor.Decrypt(KeyManager.GetKey("BundleEncryptionKey"), PaddingMode.None);
            }

            if (bundlesOffset != 0xFFFFFFFF)
            {
                accessor.Position = bundlesOffset;

                int bundleCount = accessor.ReadInt32();

                // bundle hashmap
                accessor.Position += sizeof(int) * bundleCount;

                for (int i = 0; i < bundleCount; i++)
                {
                    uint bundleOffset = accessor.ReadUInt32();

                    long curPos = accessor.Position;

                    accessor.Position = bundleOffset;

                    string name = ReadString(accessor, accessor.ReadInt32());

                    List<FileIdentifier> files = new();
                    while (true)
                    {
                        int file = accessor.ReadInt32();
                        uint fileOffset = accessor.ReadUInt32();
                        uint fileSize = accessor.ReadUInt32();

                        files.Add(new FileIdentifier(file & 0x7FFFFFFF, fileOffset, fileSize));
                        if ((file & 0x80000000) == 0)
                        {
                            break;
                        }
                    }

                    accessor.Position = curPos;

                    if (inSbIc.BundleMapping.ContainsKey(name))
                    {
                        continue;
                    }

                    BundleInfo bundle = AssetManager.AddBundle(name, inSbIc);

                    int index = 0;
                    FileIdentifier resourceInfo = files[index];

                    BlockAccessor dataAccessor = BlockAccessor.FromFile(FileSystemManager.GetFilePath(resourceInfo.FileIndex),
                        resourceInfo.Offset, (int)resourceInfo.Size);

                    BinaryBundle bundleMeta = BinaryBundle.Deserialize(dataAccessor);

                    foreach (EbxAssetEntry ebx in bundleMeta.EbxList)
                    {
                         if (dataAccessor.Position == resourceInfo.Size)
                         {
                             dataAccessor.Dispose();
                             resourceInfo = files[++index];
                             dataAccessor = BlockAccessor.FromFile(FileSystemManager.GetFilePath(resourceInfo.FileIndex),
                                 resourceInfo.Offset, (int)resourceInfo.Size);
                         }

                         uint offset = (uint)dataAccessor.Position;
                         uint size = (uint)Cas.GetCompressedSize(dataAccessor, ebx.OriginalSize);

                         ebx.AddFileInfo(new KelvinFileInfo(resourceInfo.FileIndex,
                             resourceInfo.Offset + offset, size, 0));

                         AssetManager.AddEbx(ebx, bundle.Id);
                    }
                    foreach (ResAssetEntry res in bundleMeta.ResList)
                    {
                         if (dataAccessor.Position == resourceInfo.Size)
                         {
                             dataAccessor.Dispose();
                             resourceInfo = files[++index];
                             dataAccessor = BlockAccessor.FromFile(FileSystemManager.GetFilePath(resourceInfo.FileIndex),
                                 resourceInfo.Offset, (int)resourceInfo.Size);
                         }

                         uint offset = (uint)dataAccessor.Position;
                         uint size = (uint)Cas.GetCompressedSize(dataAccessor, res.OriginalSize);

                         res.AddFileInfo(new KelvinFileInfo(resourceInfo.FileIndex,
                             resourceInfo.Offset + offset, size, 0));

                         AssetManager.AddRes(res, bundle.Id);
                    }
                    foreach (ChunkAssetEntry chunk in bundleMeta.ChunkList)
                    {
                         if (dataAccessor.Position == resourceInfo.Size)
                         {
                             dataAccessor.Dispose();
                             resourceInfo = files[++index];
                             dataAccessor = BlockAccessor.FromFile(FileSystemManager.GetFilePath(resourceInfo.FileIndex),
                                 resourceInfo.Offset, (int)resourceInfo.Size);
                         }

                         uint offset = (uint)dataAccessor.Position;
                         uint size = (uint)Cas.GetCompressedSize(dataAccessor,
                             (chunk.LogicalOffset & 0xFFFF) | chunk.LogicalSize);

                         chunk.AddFileInfo(new KelvinFileInfo(resourceInfo.FileIndex,
                             resourceInfo.Offset + offset, size, chunk.LogicalOffset));

                         AssetManager.AddChunk(chunk, bundle.Id);
                    }

                    dataAccessor.Dispose();
                }
            }

            if (chunksOffset != 0xFFFFFFFF)
            {
                accessor.Position = chunksOffset;
                int chunksCount = accessor.ReadInt32();

                // hashmap
                accessor.Position += sizeof(int) * chunksCount;

                for (int i = 0; i < chunksCount; i++)
                {
                    int offset = accessor.ReadInt32();

                    long pos = accessor.Position;
                    accessor.Position = offset;

                    Guid guid = accessor.ReadGuid();
                    int fileIndex = accessor.ReadInt32();
                    uint dataOffset = accessor.ReadUInt32();
                    uint dataSize = accessor.ReadUInt32();

                    ChunkAssetEntry chunk = new(guid, Sha1.Zero, 0, 0, inSbIc.Id);

                    chunk.AddFileInfo(new KelvinFileInfo(fileIndex, dataOffset, dataSize, 0));

                    AssetManager.AddSuperBundleChunk(chunk);

                    accessor.Position = pos;
                }
            }
        }

        return Code.Stop;
    }

    private string ReadString(DataAccessor reader, int offset)
    {
        long curPos = reader.Position;
        StringBuilder sb = new();

        do
        {
            reader.Position = offset - 1;
            string tmp = reader.ReadNullTerminatedString();
            offset = reader.ReadInt32();

            sb.Append(tmp);
        } while (offset != 0);

        reader.Position = curPos;
        return new string(sb.ToString().Reverse().ToArray());
    }
}