using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI;

public partial class MainWindow : Window
{
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = new ObservableCollection<RtmpServiceGroup>();

    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = new ObservableCollection<SelectedService>();

    private bool isReceivingStream = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadRtmpServersFromServicesJson("services.json");
        StartStreamStatusPolling();
        StartServerStatusPolling();
    }

    private async void RTMPServiceList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RTMPServiceList.SelectedItem is RtmpServiceGroup group)
        {
            var detailsWindow = new ServerDetailsWindow(group); // Skicka med MainWindow-instansen
            var result = await detailsWindow.ShowDialog<bool>(this);

            if (!result)
            {
                LogOutput.Text += $"Cancelled adding service: {group.ServiceName}\n";
            }
        }
    }

    private void RemoveSelectedService(object? sender, RoutedEventArgs e)
    {
        if (SelectedServices.SelectedItem is SelectedService service)
        {
            SelectedServicesToStream.Remove(service);
        }
    }

    private void LoadRtmpServersFromServicesJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var services = doc.RootElement.GetProperty("services");

        foreach (var service in services.EnumerateArray())
        {
            if (service.TryGetProperty("protocol", out var proto) && !proto.GetString()!.ToLower().Contains("rtmp"))
                continue;

            var serviceName = service.GetProperty("name").GetString() ?? "Unknown";

            var rtmpServers = new List<RtmpServerInfo>();

            foreach (var server in service.GetProperty("servers").EnumerateArray())
            {
                var url = server.GetProperty("url").GetString() ?? "";
                if (!url.StartsWith("rtmp")) continue;

                rtmpServers.Add(new RtmpServerInfo
                {
                    ServiceName = serviceName,
                    ServerName = server.GetProperty("name").GetString() ?? "Unnamed",
                    Url = url
                });
            }

            if (rtmpServers.Count > 0)
            {
                RtmpServiceGroups.Add(new RtmpServiceGroup
                {
                    ServiceName = serviceName,
                    Servers = rtmpServers
                });
            }
        }
    }

    private async void StartStreamStatusPolling()
    {
        while (true)
        {
            var isAlive = await Task.Run(() => CheckStreamIsAlive("rtmp://localhost/live/stream"));
            Dispatcher.UIThread.Post(() =>
            {
                StreamStatusText.Text = isAlive
                    ? "Stream status: ✅ Live"
                    : "Stream status: ❌ Not Receiving";
            });
            await Task.Delay(3000);
        }
    }

    private async void StartServerStatusPolling()
    {
        while (true)
        {
            var serverOnline = await Task.Run(() => IsRtmpServerReachable("localhost"));
            Dispatcher.UIThread.Post(() =>
            {
                ServerStatusText.Text = serverOnline
                    ? "RTMP server: ✅ Online"
                    : "RTMP server: ❌ Offline";
            });
            await Task.Delay(10000);
        }
    }

    private static bool IsRtmpServerReachable(string host, int port = 1935, int timeoutMs = 1000)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            bool connected = task.Wait(timeoutMs);
            return connected && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private unsafe bool CheckStreamIsAlive(string url, int timeoutSeconds = 5)
    {
        AVFormatContext* pFormatContext = ffmpeg.avformat_alloc_context();
        AVDictionary* options = null;

        int ret = ffmpeg.avformat_open_input(&pFormatContext, url, null, &options);
        if (ret < 0)
            return false;

        ret = ffmpeg.avformat_find_stream_info(pFormatContext, null);
        if (ret < 0)
        {
            ffmpeg.avformat_close_input(&pFormatContext);
            return false;
        }

        int videoStreamIndex = ffmpeg.av_find_best_stream(pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
        if (videoStreamIndex < 0)
        {
            ffmpeg.avformat_close_input(&pFormatContext);
            return false;
        }

        AVPacket* packet = ffmpeg.av_packet_alloc();
        bool foundFrame = false;

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            ret = ffmpeg.av_read_frame(pFormatContext, packet);
            if (ret >= 0)
            {
                if (packet->stream_index == videoStreamIndex)
                {
                    foundFrame = true;
                    break;
                }
                ffmpeg.av_packet_unref(packet);
            }
            else
            {
                Thread.Sleep(100); // Undvik tight loop
            }
        }

        ffmpeg.av_packet_free(&packet);
        ffmpeg.avformat_close_input(&pFormatContext);
        return foundFrame;
    }

    private void ToggleReceivingStream(object? sender, RoutedEventArgs e)
    {
        if (!isReceivingStream)
        {
            RtmpIncoming.IsVisible = true;
            RtmpIncoming.Play("rtmp://localhost/live/stream");

            ReceivingStatus.Text = "Receiving stream...";
            ToggleStreamButton.Content = "Stop Receiving";
            isReceivingStream = true;
        }
        else
        {
            RtmpIncoming.Stop();
            RtmpIncoming.IsVisible = false;

            ReceivingStatus.Text = "Stream stopped";
            ToggleStreamButton.Content = "Start Receiving";
            isReceivingStream = false;
        }
    }

    private List<Process>? ffmpegProcess = new();

    private async void StartStream(object? sender, RoutedEventArgs e)
    {
        if (SelectedServicesToStream.Count == 0)
            return;

        var input = "rtmp://localhost/live/stream";
        var args = new StringBuilder($"-i \"{input}\" ");

        foreach (var service in SelectedServicesToStream)
        {
            var url = service.SelectedServer.Url.TrimEnd('/');
            var streamKey = service.StreamKey;
            var fullUrl = $"{url}/{streamKey}";

            args.Append($"-c:v copy -c:a aac -f flv \"{fullUrl}\" ");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args.ToString(),
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };

            if (ffmpegProcess is not null)
                ffmpegProcess.Add(process);
            process.Start();

            // Läs FFmpeg:s standardfelutgång asynkront
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
            {
                // Logga till din UI eller debug
                Console.WriteLine(line);
                LogOutput.Text += (line + Environment.NewLine);
            }

            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FFmpeg start failed: {ex.Message}");
            LogOutput.Text += ($"FFmpeg start failed: {ex.Message}\n");
        }
    }

    private void StopStreams(object? sender, RoutedEventArgs e)
    {
        if (ffmpegProcess != null)
        {
            foreach (var process in ffmpegProcess)
            {
                process.Kill();
            }
        }
    }

    private async void OnUploadThumbnailClicked(object? sender, RoutedEventArgs e)
    {
        var window = (Window)this.VisualRoot;

        var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Thumbnail Image",
            AllowMultiple = false,
            FileTypeFilter = new List<Avalonia.Platform.Storage.FilePickerFileType>
        {
            new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
            {
                Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" }
            }
        }
        };

        var files = await window.StorageProvider.OpenFilePickerAsync(options);
        if (files != null && files.Count > 0)
        {
            var file = files[0];
            var path = file.Path.LocalPath;
            // Använd filvägen, t.ex. visa eller spara
            Console.WriteLine($"Selected thumbnail: {path}");
        }
    }

    private void OnUpdateMetadataClicked(object? sender, RoutedEventArgs e)
    {

    }
}