namespace SSMM_UI.Interfaces;

public interface IThemeService
{
    void ApplyTheme(bool darkMode);
    void ToggleTheme();
    bool IsDark { get; }
}