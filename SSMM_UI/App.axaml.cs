using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;

namespace SSMM_UI
{
    public partial class App : Application
    {
        public static LibVLC SharedLibVLC { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            SharedLibVLC = new LibVLC();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}