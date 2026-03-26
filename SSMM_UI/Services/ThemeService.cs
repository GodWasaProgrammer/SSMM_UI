using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using SSMM_UI.Interfaces;
using SSMM_UI.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SSMM_UI.Services;

public class ThemeService : IThemeService
{
    private static Application App => Application.Current!;
    private readonly StateService _stateService;
    private bool _isApplyingTheme;
    private readonly List<ThemeOption> _themes =
    [
        new("midnight", "Midnight Neon", ThemeVariant.Dark, "avares://MultistreamManager/Themes/MidnightTheme.axaml"),
        new("cyberpunk", "Cyberpunk Pulse", ThemeVariant.Dark, "avares://MultistreamManager/Themes/CyberpunkTheme.axaml"),
        new("vaporwave", "Vaporwave Drift", ThemeVariant.Dark, "avares://MultistreamManager/Themes/VaporWaveTheme.axaml"),
        new("crimson", "Crimson Core", ThemeVariant.Dark, "avares://MultistreamManager/Themes/CrimsonTheme.axaml"),
        new("aurora", "Aurora Glass", ThemeVariant.Dark, "avares://MultistreamManager/Themes/AuroraTheme.axaml"),
        new("oceanic", "Oceanic Storm", ThemeVariant.Dark, "avares://MultistreamManager/Themes/OceanicTheme.axaml"),
        new("solarflare", "Solar Flare", ThemeVariant.Dark, "avares://MultistreamManager/Themes/SolarFlareTheme.axaml"),
        new("acid", "Acid Reactor", ThemeVariant.Dark, "avares://MultistreamManager/Themes/AcidTheme.axaml"),
        new("sunrise", "Sunrise Glow", ThemeVariant.Light, "avares://MultistreamManager/Themes/SunriseTheme.axaml")
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
        foreach (var theme in _themes)
        {
            theme.PropertyChanged += OnThemeOptionPropertyChanged;
        }

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
        _isApplyingTheme = true;
        try
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

        var resourceInclude = new ResourceInclude(new Uri("avares://MultistreamManager/App.axaml"))
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
        finally
        {
            _isApplyingTheme = false;
        }
    }

    private void OnThemeOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingTheme || e.PropertyName != nameof(ThemeOption.IsSelected))
        {
            return;
        }

        if (sender is ThemeOption theme && theme.IsSelected && !theme.Key.Equals(CurrentKey, StringComparison.OrdinalIgnoreCase))
        {
            ApplyThemeInternal(theme.Key, true);
        }
    }
}
