using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SSMM_UI.Interfaces;
using SSMM_UI.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SSMM_UI.Services;

public class ThemeService : IThemeService
{
    private static Application App => Application.Current!;
    private readonly StateService _stateService;
    private readonly List<ThemeOption> _themes =
    [
        new("midnight", "Midnight Neon", ThemeVariant.Dark, "avares://SSMM_UI/Themes/MidnightTheme.axaml"),
        new("aurora", "Aurora Glass", ThemeVariant.Dark, "avares://SSMM_UI/Themes/AuroraTheme.axaml"),
        new("sunrise", "Sunrise Glow", ThemeVariant.Light, "avares://SSMM_UI/Themes/SunriseTheme.axaml")
    ];

    private UserSettings UserSettings => _stateService.UserSettingsObj;

    public bool IsDark { get; private set; }

    public string CurrentKey => CurrentTheme.Key;

    public ThemeOption CurrentTheme { get; private set; }

    public IReadOnlyList<ThemeOption> Themes => _themes;

    public ThemeService(StateService stateservice)
    {
        _stateService = stateservice;
        CurrentTheme = _themes.First();

        var preferredTheme = string.IsNullOrWhiteSpace(UserSettings.ThemeKey)
            ? CurrentKey
            : UserSettings.ThemeKey;

        ApplyThemeInternal(preferredTheme, false);
    }

    public void ApplyTheme(string themeKey) => ApplyThemeInternal(themeKey, true);

    public void ToggleTheme()
    {
        var currentIndex = _themes.FindIndex(x => x.Key.Equals(CurrentKey, StringComparison.OrdinalIgnoreCase));
        var nextIndex = currentIndex == -1 ? 0 : (currentIndex + 1) % _themes.Count;
        var nextTheme = _themes[nextIndex];
        ApplyThemeInternal(nextTheme.Key, true);
    }

    private void ApplyThemeInternal(string themeKey, bool persist)
    {
        var theme = _themes.FirstOrDefault(x => x.Key.Equals(themeKey, StringComparison.OrdinalIgnoreCase))
                    ?? _themes.First();

        CurrentTheme = theme;

        var mergedDictionaries = App.Resources.MergedDictionaries;
        var themeDictionaries = mergedDictionaries
            .OfType<ResourceInclude>()
            .Where(x => _themes.Any(t => string.Equals(t.ResourceUri, x.Source?.OriginalString, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var dict in themeDictionaries)
        {
            mergedDictionaries.Remove(dict);
        }

        var resourceInclude = new ResourceInclude(new Uri("avares://SSMM_UI/App.axaml"))
        {
            Source = new Uri(theme.ResourceUri)
        };

        mergedDictionaries.Insert(0, resourceInclude);
        App.RequestedThemeVariant = theme.Variant;
        IsDark = theme.Variant == ThemeVariant.Dark;

        foreach (var option in _themes)
        {
            option.IsSelected = option.Key.Equals(theme.Key, StringComparison.OrdinalIgnoreCase);
        }

        UserSettings.ThemeKey = theme.Key;
        UserSettings.IsDarkMode = IsDark;

        if (persist)
        {
            _stateService.SettingsChanged(UserSettings);
        }
    }
}
