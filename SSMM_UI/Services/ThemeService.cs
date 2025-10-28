using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SSMM_UI.Interfaces;
using SSMM_UI.Settings;
using System;

namespace SSMM_UI.Services;

public class ThemeService : IThemeService
{
    private static Application App => Application.Current!;
    private UserSettings UserSettings => _stateService.UserSettingsObj;
    private readonly StateService _stateService;

    public bool IsDark { get; private set; }

    public ThemeService(StateService stateservice)
    {
        _stateService = stateservice;
        ApplyTheme(UserSettings.IsDarkMode);
    }

    public void ApplyTheme(bool darkMode)
    {
        IsDark = darkMode;
        UserSettings.IsDarkMode = darkMode;

        App.RequestedThemeVariant = darkMode
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    public void ToggleTheme() => ApplyTheme(!IsDark);
}
