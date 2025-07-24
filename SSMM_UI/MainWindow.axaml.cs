using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace SSMM_UI
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> destinations = new ObservableCollection<string>();
        public MainWindow()
        {
            InitializeComponent();
            DestinationsList.ItemsSource = destinations;
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

            var process = new Process
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