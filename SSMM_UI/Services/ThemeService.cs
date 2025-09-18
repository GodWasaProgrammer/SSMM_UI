using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SSMM_UI.Interfaces;
using System;

namespace SSMM_UI.Services;

public class ThemeService : IThemeService
{
    private Application App => Application.Current!;
    private Styles? _lightTheme;
    private Styles? _darkTheme;

    public bool IsDark { get; private set; }

    public ThemeService()
    {
        LoadThemes();
    }

    private void LoadThemes()
    {
        _lightTheme = new Styles
        {
            new StyleInclude(new Uri("avares://SSMM_UI/"))
            {
                Source = new Uri("avares://SSMM_UI/Resources/LightTheme.axaml")
            }
        };

        _darkTheme = new Styles
        {
            new StyleInclude(new Uri("avares://SSMM_UI/"))
            {
                Source = new Uri("avares://SSMM_UI/Resources/DarkTheme.axaml")
            }
        };
    }

    public void ApplyTheme(bool darkMode)
    {
        App.Styles.Remove(_lightTheme!);
        App.Styles.Remove(_darkTheme!);

        // Lägg till valt tema
        App.Styles.Add(darkMode ? _darkTheme! : _lightTheme!);
        IsDark = darkMode;
        
    }

    public void ToggleTheme() => ApplyTheme(!IsDark);
}
