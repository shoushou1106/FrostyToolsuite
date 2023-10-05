using Avalonia.ThemeManager;
using Frosty.Plugin.ThemeManagerLibrary;
using System.Runtime.InteropServices;

namespace FluentThemeManager;

public class ThemePlugin : IFrostyTheme
{
    public IThemeManager ThemeManager { get => new FluentThemeManager(); }
    public string Name { get => "Fluent"; }
    public string? Author { get => "Frosty"; }
    public object? Icon { get; }
    public List<object>? Screenshots { get; }
    public string? Description { get; }
    public List<string>? Links { get; }
    public bool SupportAllPlatforms { get => false; }
    public List<OSPlatform>? SupportPlatforms { get => new() { OSPlatform.Windows }; }
}