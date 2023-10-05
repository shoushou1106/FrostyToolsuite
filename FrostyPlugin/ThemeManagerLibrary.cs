using Avalonia.ThemeManager;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Frosty.Plugin.ThemeManagerLibrary;

public interface IFrostyTheme
{
    IThemeManager ThemeManager { get; }
    string Name { get; }
    string? Author { get; }
    object? Icon { get; }
    List<object>? Screenshots { get; }
    string? Description { get; }
    List<string>? Links { get; }
    bool SupportAllPlatforms { get; }
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
