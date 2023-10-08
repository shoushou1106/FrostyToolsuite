using Avalonia.ThemeManager;
using Frosty.Plugin.ThemeManagerLibrary;
using System.Runtime.InteropServices;

namespace DefaultThemeManager;

public class ThemePlugin : IFrostyTheme
{
    public IThemeManager ThemeManager { get => new DefaultThemeManager(); }
    public string Name { get => "Default"; }
    public string? Author { get => "Frosty"; }
    public object? Icon { get; }
    public List<object>? Screenshots { get; }
    public string? Description { get; }
    public List<string>? Links { get; }
    public List<OSPlatform>? SupportPlatforms { get; }
}