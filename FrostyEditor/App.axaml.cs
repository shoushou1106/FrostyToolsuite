using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ThemeManager;
using FrostyEditor.Themes;
using FrostyEditor.Utils;
using FrostyEditor.ViewModels;
using FrostyEditor.ViewModels.Windows;
using FrostyEditor.Views.Windows;

namespace FrostyEditor;

public class App : Application
{
    public static string ConfigPath =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/Frosty/editor_config.json";
    
    public static IThemeManager? ThemeManager;

    public override void Initialize()
    {
        Config.Load(ConfigPath);

        //if (Config.Theme is null)
        //{
        //    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //    {
        //        Config.Theme = new Themes.Windows.WinDefaultThemeManager();
        //    }
        //    else
        //    {
        //        // CS8602 without this
        //        Config.Theme = new Themes.Windows.WinDefaultThemeManager();
        //    }
        //}

        //ThemeManager = Config.Theme;
        ThemeManager = new DefaultThemeManager.DefaultThemeManager();
        ThemeManager.Initialize(this);



        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktopLifetime:
            {
                ProfileSelectWindow selectWindow = new()
                {
                    DataContext = new ProfileSelectWindowViewModel()
                };

                desktopLifetime.MainWindow = selectWindow;

                break;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}