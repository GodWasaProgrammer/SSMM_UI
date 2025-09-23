using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.Interfaces;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using SSMM_UI.Views;
using System;

namespace SSMM_UI;

public partial class App : Application
{
    public static LibVLC? SharedLibVLC { get; private set; }
    public VideoPlayerService? VideoService { get; private set; }
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        SharedLibVLC = new LibVLC();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();


            Services = new ServiceCollection()
                .AddSingleton<IFilePickerService>(_ => new FilePickerService(mainWindow))
                .AddSingleton<VideoPlayerService>()
                .AddSingleton<IDialogService, DialogService>()
                .AddSingleton<MainWindowViewModel>()
                .AddSingleton<CentralAuthService>()
                .AddSingleton<MetaDataService>()
                .AddSingleton<StreamService>()
                .AddSingleton<StateService>()
                .AddSingleton<UserSettings>()
                .AddSingleton<ILogService, LogService>()
                .AddSingleton<LeftSideBarViewModel>()
                .AddSingleton<SearchViewModel>()
                .AddSingleton<DisplayStreamOutputViewModel>()
                .AddSingleton<LogViewModel>()
                .AddSingleton<SocialPosterViewModel>()
                .AddSingleton<StreamControlViewModel>()
                .AddSingleton<MetaDataViewModel>()
                .AddSingleton<BroadCastService>()
                .AddSingleton<PollService>()
                .AddSingleton<SocialPosterService>()
                .AddSingleton<SecretsAndKeysViewModel>()
                .AddSingleton<IThemeService, ThemeService>()
                .BuildServiceProvider();


            if (mainWindow.FindControl<LeftSideBarView>("LeftSideBar") is { } leftSideBar)
            {
                if (leftSideBar.FindControl<MyVideoView>("RtmpIncoming") is { } videoView)
                {
                    Services.GetRequiredService<VideoPlayerService>().RegisterVideoView(videoView);
                }
            }

            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}