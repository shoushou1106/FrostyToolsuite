using System;
using System.Diagnostics;
using System.Linq;
using Frosty.Sdk.DbObjectElements;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.Managers.Infos;
using Frosty.Sdk.Managers.Infos.FileInfos;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Managers.Loaders;

public class ManifestAssetLoader : IAssetLoader
{
    public void Load()
    {
        // This format has all SuperBundles stripped
        // all bundles and chunks of all SuperBundles are put into the manifest
        // afaik u cant reconstruct the SuperBundles, so this might make things a bit ugly
        // They also have catalog files which entries are not used, but they still make a sanity check for the offsets and indices in the file

        DbObjectDict manifest = FileSystemManager.SuperBundleManifest!;

        CasFileIdentifier file = CasFileIdentifier.FromManifestFileIdentifier(manifest.AsUInt("file"));

        string path = FileSystemManager.GetFilePath(file);

        using (BlockAccessor accessor = BlockAccessor.FromFile(path, manifest.AsUInt("offset"), manifest.AsInt("size")))
        {
            uint resourceInfoCount = accessor.ReadUInt32();
            uint bundleCount = accessor.ReadUInt32();
            uint chunkCount = accessor.ReadUInt32();

            (CasFileIdentifier, uint, long)[] files = new (CasFileIdentifier, uint, long)[resourceInfoCount];

            // resource infos
            for (int i = 0; i < resourceInfoCount; i++)
            {
                files[i] = (CasFileIdentifier.FromManifestFileIdentifier(accessor.ReadUInt32()), accessor.ReadUInt32(),
                    (uint)accessor.ReadInt64());
            }

            // bundles
            for (int i = 0; i < bundleCount; i++)
            {
                int nameHash = accessor.ReadInt32();
                int startIndex = accessor.ReadInt32();
                int resourceCount = accessor.ReadInt32();

                // unknown, always 0
                accessor.Position += sizeof(ulong);

                (CasFileIdentifier Identifier, uint Offset, long Size) resourceInfo = files[startIndex];

                // we use the installChunk of the bundle to get a superBundle and SuperBundleInstallChunk
                InstallChunkInfo ic = FileSystemManager.GetInstallChunkInfo(resourceInfo.Item1.InstallChunkIndex);
                string superbundle = ic.SuperBundles.FirstOrDefault() ?? string.Empty;
                Debug.Assert(!string.IsNullOrEmpty(superbundle), "no super bundle found for install chunk");
                // hack we just assume there are no splitSuperBundles
                SuperBundleInstallChunk sbIc = FileSystemManager.GetSuperBundleInstallChunk(superbundle);

                BinaryBundle bundleMeta;
                using (BlockAccessor bundleAccessor = BlockAccessor.FromFile(
                           FileSystemManager.GetFilePath(resourceInfo.Identifier), resourceInfo.Offset,
                           (int)resourceInfo.Size))
                {
                     bundleMeta = BinaryBundle.Deserialize(bundleAccessor);
                }

                // get name since they are hashed
                if (!ProfilesLibrary.SharedBundles.TryGetValue(nameHash, out string? name))
                {
                    foreach (EbxAssetEntry ebx in bundleMeta.EbxList)
                    {
                        // blueprint and sublevel bundles always have an ebx with the same name
                        string potentialName = ebx.Name.StartsWith(FileSystemManager.GamePlatform.ToString(), StringComparison.OrdinalIgnoreCase) ? ebx.Name : $"{FileSystemManager.GamePlatform}/{ebx.Name}";
                        int hash = Fnv1.HashString(potentialName, true);
                        if (nameHash == hash)
                        {
                            name = potentialName;
                            break;
                        }
                    }
                }

                // if we couldn't get a name just use the nameHash for now when indexing ebx the ui stuff will assign those
                if (string.IsNullOrEmpty(name))
                {
                    name = nameHash.ToString("X8");
                }

                BundleInfo bundle = AssetManager.AddBundle(name, sbIc);

                // load the assets
                // we use the file infos from the catalogs, since its easier even if they are not used by the game
                foreach (EbxAssetEntry ebx in bundleMeta.EbxList)
                {
                    ebx.AddFileInfo(ResourceManager.GetFileInfo(ebx.Sha1));

                    AssetManager.AddEbx(ebx, bundle.Id);
                }

                foreach (ResAssetEntry res in bundleMeta.ResList)
                {
                    res.AddFileInfo(ResourceManager.GetFileInfo(res.Sha1));

                    AssetManager.AddRes(res, bundle.Id);
                }

                foreach (ChunkAssetEntry chunk in bundleMeta.ChunkList)
                {
                    chunk.AddFileInfo(ResourceManager.GetFileInfo(chunk.Sha1));

                    AssetManager.AddChunk(chunk, bundle.Id);
                }
            }

            // chunks
            for (int i = 0; i < chunkCount; i++)
            {
                Guid chunkId = accessor.ReadGuid();
                (CasFileIdentifier, uint, long) resourceInfo = files[accessor.ReadInt32()];

                InstallChunkInfo ic = FileSystemManager.GetInstallChunkInfo(resourceInfo.Item1.InstallChunkIndex);
                string superbundle = ic.SuperBundles.FirstOrDefault() ?? string.Empty;
                Debug.Assert(!string.IsNullOrEmpty(superbundle), "no super bundle found for install chunk");
                // hack we just assume there are no splitSuperBundles
                SuperBundleInstallChunk sbIc = FileSystemManager.GetSuperBundleInstallChunk(superbundle);

                ChunkAssetEntry entry = new(chunkId, Sha1.Zero, 0, 0, sbIc.Id);

                entry.AddFileInfo(
                    new CasFileInfo(resourceInfo.Item1, resourceInfo.Item2, (uint)resourceInfo.Item3, 0));

                AssetManager.AddSuperBundleChunk(entry);
            }
        }
    }
}