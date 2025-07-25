using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace SSMM_UI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> destinations = [];
    private bool isReceivingStream = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ToggleReceivingStream(object? sender, RoutedEventArgs e)
    {
        if (!isReceivingStream)
        {
            // Starta mottagning av RTMP (byt till din riktiga stream-url)
            RtmpIncoming.Play("rtmp://localhost/live/stream");

            ReceivingStatus.Text = "Receiving stream...";
            ToggleStreamButton.Content = "Stop Receiving";
            isReceivingStream = true;
        }
        else
        {
            // Stoppa mottagning
            RtmpIncoming.Stop();

            ReceivingStatus.Text = "Stream stopped";
            ToggleStreamButton.Content = "Start Receiving";
            isReceivingStream = false;
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