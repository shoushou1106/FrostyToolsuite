using Avalonia.ThemeManager;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Frosty.Plugin.ThemeManagerLibrary;

/// <summary>
/// Interface for Frosty theme
/// <para>Example: </para>
/// <code>public class ThemePlugin : <see cref="IFrostyTheme"/></code>
/// <para>you can copy a example by clicking <see cref="IFrostyTheme"/> and scroll down</para>
/// </summary>
public interface IFrostyTheme
{
    /*
    
    Full example: 

    public class ThemePlugin : IFrostyTheme
    {
        public IThemeManager ThemeManager { get => new ExampleThemeManager(); }

        public string Name { get => "Example Name"; }

        public string? Author { get; }
    or
        public string? Author { get => "Example Author"; }

        public object? Icon { get; }
    or
        public object? Icon { get; }

        public List<object>? Screenshots { get; }
    or
        public List<object>? Screenshots { get => TODO; }

        public string? Description { get; }
    or
        public string? Description { get => "Example Description"; }

        public List<string>? Links { get; }
    or
        public List<string>? Links { get => new() { "https://github.com/CadeEvs/FrostyToolsuite", "https://frostytoolsuite.com/", "https://discord.gg/BXJSBzgc" }; }

        public bool SupportAllPlatforms { get => true; }

        public List<OSPlatform>? SupportPlatforms { get; }
    or
        public List<OSPlatform>? SupportPlatforms { get => new() { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.EXAMPLE }; }
    }

    */

    /// <summary>
    /// Must input a <see cref="IThemeManager"/>
    /// <para>Example: </para>
    /// <code>public <see cref="IThemeManager"/> ThemeManager { get => new ExampleThemeManager(); }</code>
    /// </summary>
//    /// <example>public IThemeManager ThemeManager { get => new ExampleThemeManager(); }</example>
    IThemeManager ThemeManager { get; }

    /// <summary>
    /// Must input a name
    /// <para>Example: </para>
    /// <code>public string Name { get => "Example Theme"; }</code>
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Must input a author
    /// <para>Example: </para>
    /// <code>public string Author { get => "Example Author"; }</code>
    /// </summary>
    string? Author { get; }

    /// <summary>
    /// Input a icon, can be <see langword="null"/>
    /// <para><see langword="null"/> Example: </para>
    /// <code>public object? Icon { get; }</code>
    /// <para>Example: </para>
    /// <code>public object Icon { get => TODO; }</code>
    /// </summary>
    object? Icon { get; }

    /// <summary>
    /// Input a list of screenshots, can be <see langword="null"/>
    /// <para><see langword="null"/> Example: </para>
//    /// <code>public List<object>? Screenshots { get; }</code>
    /// <para>Example: </para>
//    /// <code>public List<object>? Screenshots { get => TODO; }</code>
    /// </summary>
    List<object>? Screenshots { get; }

    /// <summary>
    /// Input a description, can be <see langword="null"/>
    /// <para><see langword="null"/> Example: </para>
    /// <code>public <see href="string" cref="?"/> Description { get; }</code>
    /// <para>Example: </para>
    /// <code>public <see href="string" cref="?"/> Description { get => "Example Description"; }</code>
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Input a list of links, can be <see langword="null"/>
    /// <para><see langword="null"/> Example: </para>
//    /// <code>public List<string>? Links { get; }</code>
    /// <para>Example: </para>
//    /// <code>public List<string>? Links { "https://github.com/CadeEvs/FrostyToolsuite", "https://frostytoolsuite.com/", "https://discord.gg/BXJSBzgc" }</code>
    /// </summary>
    List<string>? Links { get; }

    /// <summary>
    /// Input a list of <see cref="OSPlatform"/>, keep <see langword="null"/> for all platforms
    /// <para>All platforms(<see langword="null"/>) Example: </para>
//    /// <code>public List<OSPlatform>? SupportPlatforms { get; }</code>
    /// <para>Example: </para>
//    /// <code>public List<OSPlatform>? SupportPlatforms { get => new() { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.EXAMPLE }; }</code>
    /// </summary>
    List<OSPlatform>? SupportPlatforms { get; }
}

public static class ThemeManagerLibrary
{
    public static bool IsInitialized { get; private set; }

    public static List<IFrostyTheme> Themes = new();

    public static bool Initialize()
    {
        if (IsInitialized)
        {
            Themes = new();
        }
        
        IsInitialized = true;
        return true;
    }
}
