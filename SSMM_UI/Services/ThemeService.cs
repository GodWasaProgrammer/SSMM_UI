using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SSMM_UI.Interfaces;
using System;

namespace SSMM_UI.Services;

public class ThemeService : IThemeService
{
    private Application App => Application.Current!;

    public bool IsDark { get; private set; }

    public ThemeService()
    {
        ApplyTheme(false);
    }

    

    public void ApplyTheme(bool darkMode)
    {
        IsDark = darkMode;

        App.RequestedThemeVariant = darkMode
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    public void ToggleTheme() => ApplyTheme(!IsDark);
}
