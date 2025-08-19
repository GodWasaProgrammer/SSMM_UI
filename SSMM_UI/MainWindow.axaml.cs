using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.Puppeteering;
using SSMM_UI.Services;
using System;
using System.IO;
namespace SSMM_UI;

public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            App.Services?.GetRequiredService<StateService>().SerializeServices();
            App.Services?.GetRequiredService<StateService>().SerializeSettings();
        };
    }

    
    /// <summary>
    /// Just for debugging new functions
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void TestYThacks(object? sender, RoutedEventArgs e)
    {

        // ~~~~~~~~~~~~~~~~~~~WORKING~~~~~~~~~~~~~~//
        var videoId = "rJZZqhvgQ1A";
        var userDataDirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google",
            "Chrome",
            "User Data"
        );
        var chromeExePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
        var defaultProfilePath = Path.Combine(userDataDirPath, "Default");

        await YoutubeStudioPuppeteer.ChangeGameTitle(videoId, defaultProfilePath, chromeExePath);
        // ~~~~~~~~~~~~~~~~~~~~~~WORKING~~~~~~~~~~//
    }

    private static void DetectSystemTheme()
    {
        if (Application.Current != null)
        {
            if (Application.Current.ActualThemeVariant != null)
            {

                var isDark = Application.Current.ActualThemeVariant == ThemeVariant.Dark;

                // Ladda rätt tema
                var theme = isDark ? "Dark" : "Light";
                Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

                // Rensa befintliga resurser
                Application.Current.Resources.MergedDictionaries.Clear();

                // Lägg till det nya temat
                var themeResource = new ResourceInclude(new Uri($"avares://SSMM_UI/Resources/{theme}Theme.axaml"))
                {
                    Source = new Uri($"avares://SSMM_UI/Resources/{theme}Theme.axaml")
                };
                Application.Current.Resources.MergedDictionaries.Add(themeResource);
            }
        }
        else
        {
            throw new Exception("Our Application.Current was null. Major error");
        }
    }

}