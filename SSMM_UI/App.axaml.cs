using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
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
                .AddSingleton<StateService>()
                .AddSingleton<ILogService, LogService>()
                .AddSingleton<LeftSideBarViewModel>()
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