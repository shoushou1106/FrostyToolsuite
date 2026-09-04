using System.CommandLine;
using System.Diagnostics;
using System.Reflection;

using Frosty.Sdk;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Managers.Entries;
using Frosty.Sdk.TypeSdk;
using Frosty.Sdk.Utils;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using Sharprompt;

namespace Frosty.Cli;

internal static partial class Program
{
    private static bool s_isInteractive;

    private static int Main(string[] args)
    {
        // Logger settings are configured in appsettings.json, applies dynamically.
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());

        RootCommand rootCommand =
            new("❄ Frosty CLI, a command line app to create, update and apply mods for Frostbite Engine games.");

        FrostyLogger.Logger = factory.CreateLogger("Frosty.Cli");
        //FrostyLogger.Progress =;
        // Implement progress logging if needed
        
        // rootCommand.Arguments.Add(GameArgument);

        rootCommand.Options.Add(PidOption);
        rootCommand.Options.Add(InitFsKeyOption);
        rootCommand.Options.Add(BundleKeyOption);
        rootCommand.Options.Add(CasKeyOption);

        rootCommand.Subcommands.Add(CreateLoadCommand());
        rootCommand.Subcommands.Add(CreateModCommand());
        rootCommand.Subcommands.Add(CreateUpdateModCommand());
        rootCommand.Subcommands.Add(CreateCreateModCommand());

        rootCommand.SetAction(parseResult => InteractiveMode(
            parseResult.GetValue(InitFsKeyOption),
            parseResult.GetValue(BundleKeyOption),
            parseResult.GetValue(CasKeyOption)));

        return rootCommand.Parse(args).Invoke();
    }

    private static void InteractiveMode(FileInfo? initFsKey, FileInfo? bundleKey, FileInfo? casKey)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

#if DEBUG
        FrostyLogger.Logger.LogInformation(
            $"❄ Frosty CLI v{assembly.GetName().Version?.ToString(3) ?? "Unknown"} (Debug)");
#else
        if (assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(m => m is { Key: "Nightly", Value: "true" }))
        {
            string infoVersion =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                assembly.GetName().Version?.ToString(3) ?? "Unknown";
            FrostyLogger.Logger.LogInformation($"❄ Frosty CLI v{infoVersion} (Nightly)");
        }
        else
        {
            FrostyLogger.Logger.LogInformation($"❄ Frosty CLI v{assembly.GetName().Version?.ToString(3) ?? "Unknown"}");
        }
#endif

        if (!LoadGame(inInitFsKeyFileInfo: initFsKey, inBundleKeyFileInfo: bundleKey, inCasKeyFileInfo: casKey))
        {
            return;
        }

        InteractiveAction action;
        do
        {
            switch (action = Prompt.Select<InteractiveAction>("Select what you want to do"))
            {
                case InteractiveAction.Quit:
                    break;
                case InteractiveAction.Mod:
                    ModGame();
                    break;
                case InteractiveAction.UpdateMod:
                    UpdateMod();
                    break;
                case InteractiveAction.CreateMod:
                    CreateMod();
                    break;
                case InteractiveAction.ListEbx:
                    ListEbx();
                    break;
                case InteractiveAction.ListRes:
                    ListRes();
                    break;
                case InteractiveAction.ListChunks:
                    ListChunks();
                    break;
                case InteractiveAction.DumpEbx:
                    InteractiveDumpEbx();
                    break;
                case InteractiveAction.DumpRes:
                    InteractiveDumpRes();
                    break;
                case InteractiveAction.DumpChunks:
                    InteractiveDumpChunks();
                    break;
                case InteractiveAction.ExportEbx:
                    InteractiveExportEbx();
                    break;
                case InteractiveAction.ExportRes:
                    InteractiveExportRes();
                    break;
                case InteractiveAction.ExportChunk:
                    InteractiveExportChunk();
                    break;
            }
        } while (action != InteractiveAction.Quit);
    }

    private static bool LoadGame(FileInfo? inGameFileInfo = null, int? inPid = null,
        FileInfo? inInitFsKeyFileInfo = null, FileInfo? inBundleKeyFileInfo = null, FileInfo? inCasKeyFileInfo = null)
    {
        FileInfo? game = inGameFileInfo ?? RequestFile("Input the path to the games executable");
        
        if (game?.Exists != true)
        {
            FrostyLogger.Logger.LogError("Game does not exist.");
            return false;
        }

        // set base directory to the directory containing the executable
        Utils.BaseDirectory = Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;

        // init profile
        if (!ProfilesLibrary.Initialize(Path.GetFileNameWithoutExtension(game.Name)))
        {
            return false;
        }

        if (ProfilesLibrary.RequiresInitFsKey)
        {
            FileInfo? keyFileInfo = inInitFsKeyFileInfo ?? RequestFile("Pass in the path to an initfs key");

            if (keyFileInfo?.Exists != true)
            {
                FrostyLogger.Logger.LogError("Key does not exist.");
                return false;
            }

            if (keyFileInfo.Length != 0x10)
            {
                FrostyLogger.Logger.LogError("InitFs key needs to be 16 bytes long.");
                return false;
            }

            KeyManager.AddKey("InitFsKey", File.ReadAllBytes(keyFileInfo.FullName));
        }

        if (ProfilesLibrary.RequiresBundleKey)
        {
            FileInfo? keyFileInfo = inBundleKeyFileInfo ?? RequestFile("Pass in the path to an bundle key");

            if (keyFileInfo?.Exists != true)
            {
                FrostyLogger.Logger.LogError("Key does not exist.");
                return false;
            }

            if (keyFileInfo.Length != 0x10)
            {
                FrostyLogger.Logger.LogError("Bundle key needs to be 16 bytes long.");
                return false;
            }

            KeyManager.AddKey("BundleEncryptionKey", File.ReadAllBytes(keyFileInfo.FullName));
        }

        if (ProfilesLibrary.RequiresCasKey)
        {
            FileInfo? keyFileInfo = inCasKeyFileInfo ?? RequestFile("Pass in the path to an cas key");

            if (keyFileInfo?.Exists != true)
            {
                FrostyLogger.Logger.LogError("Key does not exist.");
                return false;
            }

            if (keyFileInfo.Length != 0x4000)
            {
                FrostyLogger.Logger.LogError("Cas key needs to be 16384 bytes long.");
                return false;
            }

            KeyManager.AddKey("CasObfuscationKey", File.ReadAllBytes(keyFileInfo.FullName));
        }

        if (game.DirectoryName is null)
        {
            FrostyLogger.Logger.LogError("The game needs to be in a directory containing the games data.");
            return false;
        }

        // init filesystem manager, this parses the layout.toc file
        if (!FileSystemManager.Initialize(game.DirectoryName))
        {
            return false;
        }

        // generate sdk if needed
        if (!File.Exists(ProfilesLibrary.SdkPath))
        {
            int pid = inPid ?? Prompt.Input<int>("Input pid of the currently running game");

            TypeSdkBuilder typeSdkBuilder = new();

            using Process process = Process.GetProcessById(pid);

            if (!typeSdkBuilder.DumpTypes(process))
            {
                return false;
            }

            FrostyLogger.Logger.LogInformation("The game is not needed anymore and can be closed.");
            if (!typeSdkBuilder.CreateSdk(ProfilesLibrary.SdkPath))
            {
                return false;
            }
        }

        // init type library, this loads the EbxTypeSdk used to properly parse ebx assets
        if (!TypeLibrary.Initialize())
        {
            return false;
        }

        // init resource manager, this parses the cas.cat files if they exist for easy asset lookup
        if (!ResourceManager.Initialize())
        {
            return false;
        }

        // init asset manager, this parses the SuperBundles and loads all the assets
        if (!AssetManager.Initialize())
        {
            return false;
        }

        s_isInteractive = true;
        return true;
    }

    private static void ListEbx()
    {
        foreach (EbxAssetEntry entry in AssetManager.EnumerateEbxAssetEntries())
        {
            Console.WriteLine(entry.Name);
        }
    }

    private static void ListRes()
    {
        foreach (ResAssetEntry entry in AssetManager.EnumerateResAssetEntries())
        {
            Console.WriteLine(entry.Name);
        }
    }

    private static void ListChunks()
    {
        foreach (ChunkAssetEntry entry in AssetManager.EnumerateChunkAssetEntries())
        {
            Console.WriteLine(entry.Name);
        }
    }

    private static FileInfo? RequestFile(string inMessage, bool inCreateDirectory = false, string? inDefaultName = null)
    {
        string path = Prompt.Input<string>(inMessage);

        return GetFile(path, inCreateDirectory, inDefaultName);
    }

    private static FileInfo? GetFile(string inPath, bool inCreateDirectory = false, string? inDefaultName = null)
    {
        if (Directory.Exists(inPath))
        {
            if (string.IsNullOrEmpty(inDefaultName))
            {
                FrostyLogger.Logger.LogError("Path can not be a Directory.");
                return null;
            }

            inPath = Path.Combine(inPath, inDefaultName);
        }

        FileInfo retVal = new(inPath);

        if (inCreateDirectory)
        {
            retVal.Directory?.Create();
        }
        else if (retVal.Directory?.Exists == false)
        {
            FrostyLogger.Logger.LogError($"Directory containing file {inPath} does not exist.");
            return null;
        }

        return retVal;
    }

    private static DirectoryInfo RequestDirectory(string inMessage, bool inCreateDirectory = false)
    {
        string path = Prompt.Input<string>(inMessage);

        DirectoryInfo retVal = new(path);

        if (inCreateDirectory)
        {
            retVal.Create();
        }

        return retVal;
    }
}