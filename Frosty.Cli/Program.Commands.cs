using System.CommandLine;

namespace Frosty.Cli;

internal static partial class Program
{
    internal static readonly Argument<FileInfo?> GameArgument = new("game-path")
    {
        Description = "The path to the game."
    };

    internal static readonly Option<int?> PidOption = new("--pid")
    {
        Description = "The pid of the game if a sdk should get generated for the game.",
        Recursive = true
    };

    /// <remarks>
    ///     Known as Key1
    /// </remarks>
    internal static readonly Option<FileInfo?> InitFsKeyOption = new("--initfs-key")
    {
        Description = "The path to a file containing a key for the InitFs if needed.",
        Recursive = true
    };

    /// <remarks>
    ///     Known as Key2
    /// </remarks>
    internal static readonly Option<FileInfo?> BundleKeyOption = new("--bundle-key")
    {
        Description = "The path to a file containing a key for Bundles if needed.",
        Recursive = true
    };

    /// <remarks>
    ///     Known as Key3
    /// </remarks>
    internal static readonly Option<FileInfo?> CasKeyOption = new("--cas-key")
    {
        Description = "The path to a file containing a key for CAS files if needed.",
        Recursive = true
    };

    private static Command CreateLoadCommand()
    {
        Command command = new("load", "Load a games data from the cache or create it.")
        {
            GameArgument
        };

        command.SetAction(parseResult => LoadGame(
            parseResult.GetResult(GameArgument) is not null ? parseResult.GetValue(GameArgument) : null,
            parseResult.GetValue(PidOption),
            parseResult.GetValue(InitFsKeyOption),
            parseResult.GetValue(BundleKeyOption),
            parseResult.GetValue(CasKeyOption)));

        return command;
    }

    private static Command CreateModCommand()
    {
        Argument<DirectoryInfo?> modsArgument = new("mods-dir")
        {
            Description = "The directory containing the mods to generate the data with."
        };
        
        Argument<DirectoryInfo?> modDataArgument = new("mod-data-dir")
        {
            Description = "The directory to generate the modded data in."
        };
        
        Command command = new("mod", "Generates a ModData folder, which can be used to mod the game.")
        {
            GameArgument,
            modsArgument,
            modDataArgument
        };

        command.SetAction(parseResult => ModGame(
            parseResult.GetResult(GameArgument) is not null ? parseResult.GetValue(GameArgument) : null,
            parseResult.GetValue(PidOption),
            parseResult.GetValue(InitFsKeyOption),
            parseResult.GetValue(BundleKeyOption),
            parseResult.GetValue(CasKeyOption),
            parseResult.GetValue(modsArgument),
            parseResult.GetValue(modDataArgument)));

        return command;
    }

    private static Command CreateUpdateModCommand()
    {
        Argument<FileInfo?> modArgument = new("mod-path")
        {
            Description = "The path to the mod that should get updated."
        };

        Option<string?> outputOption = new("--output")
        {
            Description = "The path where the updated mod should be saved to, defaults to the input path."
        };
        
        Command command = new("update-mod", "Updates a .fbmod to the newest version.")
        {
            GameArgument,
            modArgument,
            outputOption
        };
        
        command.SetAction(parseResult => UpdateMod(
            parseResult.GetResult(GameArgument) is not null ? parseResult.GetValue(GameArgument) : null,
            parseResult.GetValue(PidOption),
            parseResult.GetValue(InitFsKeyOption),
            parseResult.GetValue(BundleKeyOption),
            parseResult.GetValue(CasKeyOption),
            parseResult.GetValue(modArgument),
            parseResult.GetValue(outputOption)));

        return command;
    }

    private static Command CreateCreateModCommand()
    {
        Argument<DirectoryInfo?> projectArgument = new("project-path")
        {
            Description = "The path to the project directory that should get updated."
        };

        Option<string?> outputOption = new("--output")
        {
            Description = "The path where the created mod should be saved to, defaults to the project path with the .fbmod extension."
        };

        Command command = new("create-mod", "Creates a mod from a project.")
        {
            GameArgument,
            projectArgument,
            outputOption
        };

        command.SetAction(parseResult => CreateMod(
            parseResult.GetResult(GameArgument) is not null ? parseResult.GetValue(GameArgument) : null,
            parseResult.GetValue(PidOption),
            parseResult.GetValue(InitFsKeyOption),
            parseResult.GetValue(BundleKeyOption),
            parseResult.GetValue(CasKeyOption),
            parseResult.GetValue(projectArgument),
            parseResult.GetValue(outputOption)));

        return command;
    }
}