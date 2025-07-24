using Avalonia.Controls;
using Avalonia.Interactivity;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace SSMM_UI
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> destinations = [];
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        public MainWindow()
        {
            InitializeComponent();

            DestinationsList.ItemsSource = destinations;

            var currentDir = System.AppContext.BaseDirectory; // där din app körs
            var libVlcPath = System.IO.Path.Combine(currentDir, "runtimes", "win-x64", "native");
            Core.Initialize();

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.MediaPlayer = _mediaPlayer;

            var media = new Media(_libVLC, "rtmp://localhost/live/stream", FromType.FromLocation);
            _mediaPlayer.Play(media);
        }
        private void AddDestination(object? sender, RoutedEventArgs e)
        {
            var url = NewUrlBox.Text;
            if (!string.IsNullOrWhiteSpace(url))
            {
                destinations.Add(url); // Lägg till i ObservableCollection
                NewUrlBox.Text = string.Empty;
            }
        }

        private async void StartStream(object? sender, RoutedEventArgs e)
        {
            if (destinations.Count == 0) return;

            var input = "rtmp://localhost/live/stream";
            var args = new StringBuilder($"-i {input} ");

            foreach (var dst in destinations)
                args.Append($"-c:v copy -f flv {dst} ");

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args.ToString(),
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        }
    }
}