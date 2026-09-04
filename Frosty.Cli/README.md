# Frosty CLI

## Installation

### Build from source

#### Install dependencies
This project requires [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/install/)

#### Build instructions
Follow the [build instructions](https://github.com/FrostyToolsuite/FrostyToolsuite?tab=readme-ov-file#from-source).

The compiled executable will be in the `Frosty.Cli/bin/Debug/net10.0/` directory.

## Nightly builds
Grab the latest CLI build for Windows or Linux from the [GitHub Actions](https://github.com/FrostyToolsuite/FrostyToolsuite/actions), compiled from the latest commit.

On Linux, set FrostyCli to be executable:
```bash
chmod +x FrostyCli
```

On macOS, remove the [Gatekeeper](https://support.apple.com/guide/security/gatekeeper-and-runtime-protection-sec5599b66df) and set FrostyCli to be executable before using it:
```zsh
xattr -d com.apple.quarantine FrostyCli
chmod +x FrostyCli
```

## Usage

### Overview
> [!NOTE]
> Mods made with Frosty 1.0.x must be converted using the UpdateMod option in interactive mode or with the update-mod argument before use with FrostyCLI.
```
Description:
  ❄ Frosty CLI, a command line app to create, update and apply mods for Frostbite Engine games.

Usage:
  Frosty.Cli [command] [options]

Options:
  --pid <pid>                The pid of the game if a sdk should get generated for the game.
  --initfs-key <initfs-key>  The path to a file containing a key for the InitFs if needed.
  --bundle-key <bundle-key>  The path to a file containing a key for Bundles if needed.
  --cas-key <cas-key>        The path to a file containing a key for CAS files if needed.
  -?, -h, --help             Show help and usage information
  --version                  Show version information

Commands:
  load <game-path>                       Load a games data from the cache or create it.
  mod <game-path> <mods-dir>             Generates a ModData folder, which can 
  <mod-data-dir>                         be used to mod the game.
  update-mod <game-path> <mod-path>      Updates a .fbmod to the newest version.
  create-mod <game-path> <project-path>  Creates a mod from a project.

```

### Interactive mode
Using the interactive CLI mode:
```bash
$ ./FrostyCli
```
Example clip using the interactive mode to generate mod data:

![Frosty CLI Interactive Mode](../Resources/FrostyCLIDemo.gif)

After generating a mod data folder, pass the `datapath` argument to the game's launch options to apply the mods as such:

```
-dataPath "<mod data path>"
```

The dataPath argument takes either absolute paths or paths relative to the games directory. For example, if your mod data folder is in the games folder, it'd look like:

```
-dataPath "Moddata"
```

Or as an alternative to the `datapath` launch command, you can use the `GAME_DATA_DIR` environment variable instead as such:

```
GAME_DATA_DIR=<mod data path>
```

> [!NOTE]
> Games using the Frostbite version above 2014.4.11 require the following steps (check the version in the game's JSON file in the Profiles folder) 

#### Linux and macOS
Please copy the bcrypt.dll file from the ThirdParty folder and paste it into the game's folder and add the wine DLL override before the `datapath` command as such:
```WINEDLLOVERRIDES="bcrypt=n,b" %command% -datapath '<mod data path>'```

#### Windows
For Windows users, please copy the CryptBase.dll file from the ThirdParty folder and paste it into the game's folder.
