using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.MetaData;
using SSMM_UI.Services;

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
                .AddSingleton<StateService>()
                .AddSingleton<ILogService, LogService>()
                .BuildServiceProvider();

            // Hämta video view från MainWindow och registrera
            if (mainWindow.FindControl<MyVideoView>("RtmpIncoming") is { } videoView)
            {
                Services.GetRequiredService<VideoPlayerService>().RegisterVideoView(videoView);
            }

            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}