using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.Services;

namespace SSMM_UI
{
    public partial class App : Application
    {
        public static LibVLC? SharedLibVLC { get; private set; }
        // Deklarera _services fältet här
        private IServiceProvider? _services;

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

                var services = new ServiceCollection()
                    .AddSingleton<IFilePickerService>(_ => new FilePickerService(mainWindow))
                    .AddSingleton<IDialogService, DialogService>()
                    .AddSingleton<MainWindowViewModel>(provider =>
                        new MainWindowViewModel(
                            provider.GetRequiredService<IDialogService>(),
                            provider.GetRequiredService<IFilePickerService>()
                        ))
                    .BuildServiceProvider();

                mainWindow.DataContext = services.GetRequiredService<MainWindowViewModel>();
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}