using Avalonia.ThemeManager;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System;
using MsBox.Avalonia;

namespace Frosty.Plugin.ThemeManagerLibrary;

/// <summary>
/// Interface for Frosty theme
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

    IThemeManager ThemeManager { get; }
    string Name { get; }
    string? Author { get; }
    object? Icon { get; }
    List<object>? Screenshots { get; }
    string? Description { get; }
    List<string>? Links { get; }

    /// <summary>
    /// Set to null means support all platforms
    /// </summary>
    List<OSPlatform>? SupportPlatforms { get; }
}

public class ThemeManagerLibrary
{
    public static bool IsInitialized { get; private set; }

    public static List<IFrostyTheme> Themes = new();

    public static bool Initialize()
    {
        if (IsInitialized)
        {
            Themes = new();
        }

        string themePluginFolder = Directory.GetCurrentDirectory() + "\\Themes\\"; // TODO: Move this to config
        foreach (var themePluginPath in Directory.GetFiles(themePluginFolder))
        {
            try
            {
                Assembly themePluginAssembly = Assembly.LoadFile(themePluginPath);
                Type? themePlugin = themePluginAssembly.GetType(themePluginAssembly.GetName().Name + ".ThemePlugin");
                if (themePlugin != null)
                {
                    if (typeof(IFrostyTheme).IsAssignableFrom(themePlugin))
                    {
                        IFrostyTheme? result = Activator.CreateInstance(themePlugin) as IFrostyTheme;
                        if (result != null)
                        {
                            Themes.Add(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: Exception box
                var MessageBox = MessageBoxManager.GetMessageBoxStandard("Failed to load theme", ex.Message).ShowAsync;
            }
        }

        IsInitialized = true;
        return true;
    }
}
