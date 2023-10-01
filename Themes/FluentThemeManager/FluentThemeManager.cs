using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.ThemeManager;
using Avalonia.Themes.Fluent;
using FluentThemeManager.Styles;

namespace FluentThemeManager;

public class FluentThemeManager : IThemeManager
{
    private static readonly Uri s_baseUri = new("avares://FluentThemeManager/Styles");

    private static readonly FluentTheme s_fluent = new();

    private static readonly DockTheme s_dockFluent = new();

    private static readonly TreeDataGridTheme s_treeDataGridFluent = new();

    private static readonly Avalonia.Styling.Styles s_fluentDark = new()
    {
        new StyleInclude(s_baseUri)
        {
            Source = new Uri("avares://FluentThemeManager/Styles/FluentDark.axaml")
        }
    };

    private static readonly Avalonia.Styling.Styles s_fluentLight = new()
    {
        new StyleInclude(s_baseUri)
        {
            Source = new Uri("avares://FluentThemeManager/Styles/FluentLight.axaml")
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
                    Application.Current.Styles[0] = s_fluent;
                    Application.Current.Styles[1] = s_dockFluent;
                    Application.Current.Styles[2] = s_treeDataGridFluent;
                    Application.Current.Styles[3] = s_fluentLight;
                    break;
                }
            // Fluent Dark
            case 1:
                {
                    Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                    Application.Current.Styles[0] = s_fluent;
                    Application.Current.Styles[1] = s_dockFluent;
                    Application.Current.Styles[2] = s_treeDataGridFluent;
                    Application.Current.Styles[3] = s_fluentDark;
                    break;
                }
        }
    }

    public void Initialize(Application application)
    {
        application.RequestedThemeVariant = ThemeVariant.Dark;
        application.Styles.Insert(0, s_fluent);
        application.Styles.Insert(1, s_dockFluent);
        application.Styles.Insert(2, s_treeDataGridFluent);
        application.Styles.Insert(3, s_fluentDark);
    }
}
