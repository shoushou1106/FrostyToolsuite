using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Frosty.Sdk.IO;
using Frosty.Sdk.IO.Compression;
using Frosty.Sdk.Managers.CatResources;
using Frosty.Sdk.Managers.Infos;
using Frosty.Sdk.Managers.Infos.FileInfos;
using Frosty.Sdk.Utils;
using Microsoft.Extensions.Logging;

namespace Frosty.Sdk.Managers;

public static class ResourceManager
{
    public static bool IsInitialized { get; private set; }

    private static readonly Dictionary<Sha1, CasFileInfo> s_resourceEntries = new();

    private static readonly List<CatPatchEntry> s_patchEntries = new();

    private static readonly Dictionary<Sha1, uint> s_sizeMap = new();

    public static void LoadInstallChunks()
    {
        foreach (InstallChunkInfo installChunkInfo in FileSystemManager.EnumerateInstallChunks())
        {
            LoadInstallChunk(installChunkInfo);
        }
    }

    public static void CLearInstallChunks()
    {
        s_resourceEntries.Clear();
        s_patchEntries.Clear();
    }

    public static bool Initialize()
    {
        if (IsInitialized)
        {
            return true;
        }

        if (!FileSystemManager.IsInitialized)
        {
            FrostyLogger.Logger.LogError("FileSystemManager isn't initialized.");
            return false;
        }

        if (FileSystemManager.HasFileInMemoryFs("Scripts/CasEncrypt.yaml"))
        {
            // load CasEncrypt.yaml from memoryFs (used for decrypting data in cas files)
            using (TextReader stream = new StreamReader(FileSystemManager.GetFileFromMemoryFs("Scripts/CasEncrypt.yaml").ToStream()))
            {
                byte[]? key = null;
                while (stream.Peek() != -1)
                {
                    string line = stream.ReadLine()!;
                    if (line.Contains("keyid:"))
                    {
                        string[] arr = line.Split(':');
                        KeyManager.AddKey(arr[1].Trim(), key!);
                    }
                    else if (line.Contains("key:"))
                    {
                        string[] arr = line.Split(':');
                        string keyStr = arr[1].Trim();

                        key = new byte[keyStr.Length / 2];
                        for(int i = 0; i < keyStr.Length / 2; i++)
                        {
                            key[i] = Convert.ToByte(keyStr.Substring(i * 2, 2), 16);
                        }
                    }
                }
            }
        }

        // get oodle libs from unreal
        if (Directory.EnumerateFiles(FileSystemManager.BasePath, "oo2core_*").Any() &&
            !File.Exists(CompressionOodle.LibraryPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CompressionOodle.LibraryPath)!);
            if (!TryDownloadOodle(CompressionOodle.LibraryPath))
            {
                FrostyLogger.Logger.LogError(
                    "Failed to download Oodle. Please install Oodle 2.9.16 or equivalent from Unreal Engine manually, and name it liboo2core.");
                FrostyLogger.Logger.LogInformation("https://github.com/WorkingRobot/OodleUE/releases/2026-06-04-1357/");
                return false;
            }
        }

        IsInitialized = true;
        return true;
    }

    /// <summary>
    ///     Downloads Oodle for the current OS and CPU into <paramref name="inDestination" />
    /// </summary>
    private static bool TryDownloadOodle(string inDestination)
    {
        bool isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (!isArm64 && RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            FrostyLogger.Logger.LogError(
                "Auto Oodle download does not support your current architecture: {Arch}", RuntimeInformation.ProcessArchitecture.ToString());
            return false;
        }

        try
        {
            using HttpClient client = new();
            if (OperatingSystem.IsWindows())
            {
                return isArm64
                    ? TryExtractOodle(client, "msvc-arm64-release.zip",
                        "dc6c93af3b8dc4d7a176d838487aa5d0f540aad94197e296d5adabad0b0a8e8e", "bin/oodle-data-shared.dll",
                        inDestination)
                    : TryExtractOodle(client, "msvc-x64-release.zip",
                        "08844ec260e40134ce6089ffe2003d6f862cd2abf1ebfb62a3fa8d2f69f48a2b", "bin/oodle-data-shared.dll",
                        inDestination);
            }

            if (OperatingSystem.IsLinux())
            {
                return isArm64
                    ? TryExtractOodle(client, "gcc-arm64-release.zip",
                        "fb698deb4f725ade94e2dfe16d9628e13a6a699cc21fc4c3d742bd061a065f40", "lib/liboodle-data-shared.so",
                        inDestination)
                    : TryExtractOodle(client, "gcc-x64-release.zip",
                        "458c66945ea6124d4f6fcf64cec5601ab230f61f6677a6ecab6cca621a9d0e11", "lib/liboodle-data-shared.so",
                        inDestination);
            }

            if (!OperatingSystem.IsMacOS())
            {
                return false;
            }

            // TODO: Pick the Oodle version per game instead of one fixed version, see CompressionOodle.Compress.
            // macOS release does not ship inside the release zip.
            // Windows release is not included in the repo directly.
            Uri oodleMacLibrary = new(
                "https://raw.githubusercontent.com/WorkingRobot/OodleUE/2026-06-04-1357/Engine/Source/Runtime/OodleDataCompression/Sdks/2.9.16/lib/Mac/liboo2coremac64.2.9.16.dylib");

            const string oodleMacLibrarySha256 =
                "47269bc3c9f7ef7948775be2c018ce674d1ab8a8526b6935e9d1033b0b47e8b7";

            
            byte[]? library = TryDownloadVerified(client, oodleMacLibrary, oodleMacLibrarySha256);
            if (library is null)
            {
                return false;
            }

            File.WriteAllBytes(inDestination, library);
            return true;
        }
        catch (HttpRequestException e)
        {
            FrostyLogger.Logger.LogError(e, "Failed to download Oodle from the OodleUE release.");
            return false;
        }
    }

    private static bool TryExtractOodle(HttpClient inClient, string inAsset, string inSha256, string inEntry,
        string inDestination)
    {
        // TODO: Pick the Oodle version per game instead of one fixed version, see CompressionOodle.Compress.
        Uri oodleRelease =
            new("https://github.com/WorkingRobot/OodleUE/releases/download/2026-06-04-1357/");

        byte[]? zip = TryDownloadVerified(inClient, new Uri(oodleRelease, inAsset), inSha256);
        if (zip is null)
        {
            return false;
        }

        using ZipArchive archive = new(new MemoryStream(zip), ZipArchiveMode.Read);
        ZipArchiveEntry? entry = archive.GetEntry(inEntry);
        if (entry is null)
        {
            return false;
        }

        entry.ExtractToFile(inDestination, true);
        return true;
    }

    /// <returns>The downloaded bytes, or <see langword="null" /> if they do not match <paramref name="inSha256" />.</returns>
    private static byte[]? TryDownloadVerified(HttpClient inClient, Uri inUri, string inSha256)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, inUri);
        using HttpResponseMessage response = inClient.Send(request);
        response.EnsureSuccessStatusCode();

        using MemoryStream download = new();
        response.Content.ReadAsStream().CopyTo(download);
        byte[] bytes = download.ToArray();
        if (SHA256.HashData(bytes).AsSpan().SequenceEqual(Convert.FromHexString(inSha256)))
        {
            return bytes;
        }

        FrostyLogger.Logger.LogError("Failed to verify. {Uri} does not match its expected hash.", inUri);
        return null;
    }

    public static long GetSize(Sha1 sha1)
    {
        return s_sizeMap.TryGetValue(sha1, out uint size) ? size : -1;
    }

    public static CasFileInfo? GetPatchFileInfo(Sha1 sha1, Sha1 deltaSha1, Sha1 baseSha1)
    {
        if (s_resourceEntries.TryGetValue(sha1, out CasFileInfo? fileInfo))
        {
            return fileInfo;
        }

        CasFileInfo? baseInfo = s_resourceEntries.GetValueOrDefault(baseSha1);
        CasFileInfo? deltaInfo = s_resourceEntries.GetValueOrDefault(deltaSha1);

        if (baseInfo is null || deltaInfo is null)
        {
            return null;
        }

        fileInfo = new CasFileInfo(baseInfo.GetBase(), deltaInfo.GetBase());

        s_resourceEntries.TryAdd(sha1, fileInfo);

        return fileInfo;
    }

    public static CasFileInfo? GetFileInfo(Sha1 sha1)
    {
        return s_resourceEntries.GetValueOrDefault(sha1);
    }

    private static void LoadInstallChunk(InstallChunkInfo info)
    {
        Dictionary<Sha1, CasFileInfo> infos = new();
        foreach (FileSystemSource source in FileSystemManager.Sources)
        {
            LoadEntries(info, source, infos);
        }

        foreach (CatPatchEntry entry in s_patchEntries)
        {
            infos.TryGetValue(entry.BaseSha1, out CasFileInfo? baseFileInfo);

            infos.TryGetValue(entry.DeltaSha1, out CasFileInfo? deltaFileInfo);

            Debug.Assert(deltaFileInfo is not null, "No delta entry!");

            CasFileInfo fileInfo = new(baseFileInfo?.GetBase(), deltaFileInfo?.GetBase());
            s_resourceEntries.TryAdd(entry.Sha1, fileInfo);
        }
        s_patchEntries.Clear();
    }

    private static void LoadEntries(InstallChunkInfo info, FileSystemSource inSource,
        Dictionary<Sha1, CasFileInfo> retVal)
    {
        if (!inSource.TryResolvePath(Path.Combine(info.InstallBundle, "cas.cat"), out string? filePath))
        {
            return;
        }

        uint installChunkIndex = FileSystemManager.GetInstallChunkIndex(info);
        bool patch = inSource.Path == FileSystemSource.Patch.Path;

        using (CatReader stream = new(filePath))
        {
            for (int i = 0; i < stream.ResourceCount; i++)
            {
                CatResourceEntry entry = stream.ReadResourceEntry();
                CasFileIdentifier casFileIdentifier = new(patch, installChunkIndex, entry.ArchiveIndex);

                CasFileInfo fileInfo = new(casFileIdentifier, entry.Offset, entry.Size, entry.LogicalOffset);

                if (!s_resourceEntries.TryAdd(entry.Sha1, fileInfo) && fileInfo.IsComplete() && !s_resourceEntries[entry.Sha1].IsComplete())
                {
                    s_resourceEntries[entry.Sha1] = fileInfo;
                }

                if (!s_sizeMap.TryAdd(entry.Sha1, entry.Size))
                {
                    s_sizeMap[entry.Sha1] = Math.Max(s_sizeMap[entry.Sha1], entry.Size);
                }

                retVal.TryAdd(entry.Sha1, fileInfo);
            }

            for (int i = 0; i < stream.EncryptedCount; i++)
            {
                CatResourceEntry entry = stream.ReadEncryptedEntry();
                CasFileIdentifier casFileIdentifier = new(patch, installChunkIndex, entry.ArchiveIndex);

                CasFileInfo fileInfo = new(casFileIdentifier, entry.Offset, entry.Size, entry.LogicalOffset,
                    entry.KeyId);

                if (!s_resourceEntries.TryAdd(entry.Sha1, fileInfo) && fileInfo.IsComplete() && !s_resourceEntries[entry.Sha1].IsComplete())
                {
                    s_resourceEntries[entry.Sha1] = fileInfo;
                }

                if (!s_sizeMap.TryAdd(entry.Sha1, entry.Size))
                {
                    s_sizeMap[entry.Sha1] = Math.Max(s_sizeMap[entry.Sha1], entry.Size);
                }

                retVal.TryAdd(entry.Sha1, fileInfo);
            }

            for (int i = 0; i < stream.PatchCount; i++)
            {
                CatPatchEntry entry = stream.ReadPatchEntry();
                s_patchEntries.Add(entry);
            }
        }
    }
}