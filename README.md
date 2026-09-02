<img align="center" src="Resources/FrostyBannerChucky296.svg" alt="Frosty Banner">

<p align="center">
  <a title="Discord Server" href="https://discord.gg/sB8ZUAT">
    <img alt="Discord Server" src="https://img.shields.io/discord/333086156478480384?color=green&label=DISCORD&logo=discord&logoColor=white">
  </a>
  <a title="Total Downloads" href="https://github.com/CadeEvs/FrostyToolsuite/releases/latest">
    <img alt="Total Downloads" src="https://img.shields.io/github/downloads/CadeEvs/FrostyToolsuite/latest/total?color=white&label=DOWNLOADS&logo=github">
  </a>
</p>

> [!WARNING]
> This Project is in development and doesn't have a release yet.
> 
> **Use at your own risk.**
> 
> For a stable release use [FrostyToolsuite v1.0.6.3](https://github.com/CadeEvs/FrostyToolsuite/releases/latest).

# About
The FrostyToolsuite is a modding tool for games running on DICE's Frostbite game engine.

This is a rewrite of the FrostyToolsuite in modern .NET, which is in early development and has no functional UI yet.

The old repository can be found at https://github.com/CadeEvs/FrostyToolsuite.

The goal of this rewrite is to clean up the code base and make it cross-platform with the use of [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) and the [MVVM Community Toolkit](https://aka.ms/mvvmtoolkit/docs) instead of WPF.

## Structure
The Toolsuite is split up into multiple projects.

### Frosty.Editor
A GUI application that is used to create mods.

### Frosty.ModManager
A GUI application that is used to select what mods to apply to the game.

### [Frosty.Cli](Frosty.Cli/README.md)
A CLI application that is used to create, update and apply mods.

### Frosty.Sdk
A library that is used to access data from the game. Contains the source generator used to improve the type sdk which gets dumped from the game's memory.

### Frosty.Sdk.Tests
Unit tests for the sdk.

### Frosty.Modding
A library that is used to create modified data which the game can read. Formerly `FrostyModSupport`.

## Data Location
Game profiles, type sdk strings shipped next to the executable and are read-only.
Settings, anything Frosty generates, or the user creates are inside a per-user directory.

| Platform | Location                                        |
|----------|-------------------------------------------------|
| Windows  | `%APPDATA%\FrostyToolsuite`                     |
| macOS    | `~/Library/Application Support/FrostyToolsuite` |
| Linux    | `~/.config/FrostyToolsuite`                     |

# Getting Started

## Release
Download the latest release from [releases](https://github.com/FrostyToolsuite/FrostyToolsuite/releases/latest).

# Developers

## Build from source
Make sure you have [Git](https://git-scm.com/downloads) and the [.NET SDK](https://learn.microsoft.com/en-us/dotnet/core/install/) installed and in your path.

Clone and build using these commands.
```
git clone https://github.com/FrostyToolsuite/FrostyToolsuite.git
cd FrostyToolsuite
dotnet build
```
After that the executable for the CLI can be found in `Frosty.Cli/bin/Debug/net10.0`.

*This is just an example, you can use any way you want to clone this repo*

# Documentation
Todo

# Plugins
Todo

# Contributing
If you want to contribute to Frosty, you can fork this branch and make a pull request with your changes.
Before you do that, please read the [.NET Framework design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) and make sure you follow it.
In the [Projects tab](https://github.com/orgs/FrostyToolsuite/projects/1) you can see what needs to be done, ideas of what can be done and stuff that is currently getting worked on or is already done.
If you decide to work on something, it would be great if you could say that in the #developer channel on our [Discord server](https://discord.gg/sB8ZUAT). Make sure to read the #read-me channel after joining it
