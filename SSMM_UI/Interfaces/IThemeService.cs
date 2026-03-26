using SSMM_UI.Settings;
using System.Collections.Generic;

namespace SSMM_UI.Interfaces;

public interface IThemeService
{
    void ApplyTheme(string themeKey);
    void ToggleTheme();
    bool IsDark { get; }
    string CurrentKey { get; }
    IReadOnlyList<ThemeOption> Themes { get; }
}
