using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.ThemeManager;
using Avalonia.Themes.Simple;
using FrostyTheme;

namespace DefaultThemeManager;

public class DefaultThemeManager : IThemeManager
{
    private static readonly Uri s_baseUri = new("avares://DefaultThemeManager/Styles");

    private static readonly SimpleTheme s_simple = new();

    private static readonly DockTheme s_dock = new();

    private static readonly TreeDataGridTheme s_treeDataGrid = new();

    private static readonly Styles s_simpleDark = new()
    {
        new StyleInclude(s_baseUri)
        {
            Source = new Uri("avares://DefaultThemeManager/FluentDark.axaml")
        }
    };

    private static readonly Styles s_simpleLight = new()
    {
        new StyleInclude(s_baseUri)
        {
            Source = new Uri("avares://DefaultThemeManager/FluentLight.axaml")
        }
    };

    public void Switch(int index)
    {
        if (Application.Current is null)
        {
            return;
        }

        switch (index)
        {
            // Fluent Light
            case 0:
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                    Application.Current.Styles[0] = s_simple;
                    Application.Current.Styles[1] = s_dock;
                    Application.Current.Styles[2] = s_treeDataGrid;
                    Application.Current.Styles[3] = s_simpleLight;
                    break;
                }
            // Fluent Dark
            case 1:
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    Application.Current.Styles[0] = s_simple;
                    Application.Current.Styles[1] = s_dock;
                    Application.Current.Styles[2] = s_treeDataGrid;
                    Application.Current.Styles[3] = s_simpleDark;
                    break;
                }
        }
    }

    public void Initialize(Application application)
    {
        application.RequestedThemeVariant = ThemeVariant.Dark;
        application.Styles.Insert(0, s_simple);
        application.Styles.Insert(1, s_dock);
        application.Styles.Insert(2, s_treeDataGrid);
        application.Styles.Insert(3, s_simpleDark);
    }
}
