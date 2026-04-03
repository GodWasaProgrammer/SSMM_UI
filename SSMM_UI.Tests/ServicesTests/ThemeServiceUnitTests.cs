using SSMM_UI.Settings;

namespace SSMM_UI.Tests.ServicesTests;

public class ThemeServiceUnitTests
{
    [Fact]
    public void Themes_ShouldContainExpectedKeys()
    {
        var themes = new ThemeOption[]
        {
            new("midnight", "Midnight Neon", Avalonia.Styling.ThemeVariant.Dark, "x"),
            new("sunrise", "Sunrise Glow", Avalonia.Styling.ThemeVariant.Light, "y")
        };

        Assert.Contains(themes, t => t.Key == "midnight");
        Assert.Contains(themes, t => t.Key == "sunrise");
    }
}
