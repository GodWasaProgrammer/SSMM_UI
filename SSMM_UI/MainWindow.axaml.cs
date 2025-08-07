using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using FFmpeg.AutoGen;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace SSMM_UI;

public partial class MainWindow : Window
{
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; private set; }
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; private set; }
    private CentralAuthService _centralAuthService;
    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

    private readonly StateService _stateService = new();
    private readonly StreamService _streamService;
    public RTMPServer Server { get; set; } = new();
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";
    private MetaDataService? _metaDataService { get; set; }
    private bool isReceivingStream = false;
    private readonly List<Process>? ffmpegProcess = [];

    public MainWindow()
    {
        InitializeComponent();
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;
        DataContext = this;
        _centralAuthService = new CentralAuthService();
        _streamService = new(_centralAuthService);

        StartStreamStatusPolling();
        StartServerStatusPolling();
        if (!Design.IsDesignMode)
            RtmpIncoming.Play(RtmpAdress);
        RunRTMPServer();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await AutoLoginIfTokenized();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _stateService.SerializeServices();
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
                LogOutput.CaretIndex = LogOutput.Text.Length;
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

    private async void StartStreamStatusPolling()
    {
        while (true)
        {
            var isAlive = await Task.Run(() => CheckStreamIsAlive(RtmpAdress));
            Dispatcher.UIThread.Post(() =>
            {
                StreamStatusText.Text = isAlive
                    ? "Stream status: ✅ Live"
                    : "Stream status: ❌ Not Receiving";
            });
        }
    }

    private void RunRTMPServer()
    {
        Server.SetupServerAsync();
    }

    private async void StartServerStatusPolling()
    {
        while (true)
        {
            bool isResponding = await IsRtmpApiResponding(); // Använd await istället för .Result

            ServerStatusText.Text = isResponding
                ? "RTMP-server: ✅ Running"
                : "RTMP-server: ❌ Inte startad";

            await Task.Delay(5000); // 5 sekunders delay
        }
    }

    private async Task<bool> IsRtmpApiResponding()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(25); // Sänk timeout till 2 sekunder
            var response = await client.GetAsync("https://localhost:7000/ui/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task AutoLoginIfTokenized()
    {
        var authService = new CentralAuthService();
        var results = await authService.TryAutoLoginAllAsync();

        foreach (var result in results)
        {
            var message = result.Success
                ? $"✅ Logged in as: {result.Username}"
                : $"❌ {result.ErrorMessage}";

            switch (result.Provider)
            {
                case AuthProvider.Twitch:
                    TwitchLogin.Text = message;
                    break;
                case AuthProvider.YouTube:
                    LoginStatusText.Text = message;
                    break;
                case AuthProvider.Kick:
                    KickLogin.Text = message;
                    break;
            }
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
            ReceivingStatus.Text = "Receiving stream...";
            ToggleStreamButton.Content = "Stop Receiving";
            isReceivingStream = true;
        }
        else
        {
            //RtmpIncoming.Stop();
            RtmpIncoming.IsVisible = false;

            ReceivingStatus.Text = "Stream stopped";
            ToggleStreamButton.Content = "Start Receiving";
            isReceivingStream = false;
        }
    }

    //private async Task SetYouTubeCategoryAndGameAsync(string videoId, string wikipediaUrl, string accessToken)
    //{
    //    // 1. Uppdatera kategorin till "Gaming" via klient-API
    //    var videosList = _youtubeService.Videos.List("snippet,topicDetails"); // ändrat här
    //    videosList.Id = videoId;

    //    var video = (await videosList.ExecuteAsync()).Items.FirstOrDefault();

    //    if (video == null)
    //    {
    //        Console.WriteLine("Kunde inte hitta videon med angivet ID.");
    //        return;
    //    }

    //    video.Snippet.CategoryId = "20"; // Gaming

    //    var updateRequest = _youtubeService.Videos.Update(video, "snippet");
    //    await updateRequest.ExecuteAsync();

    //    // 2. Gör PATCH-anrop för att sätta spel/topicDetails
    //    var patchPayload = new
    //    {
    //        id = videoId,
    //        topicDetails = new
    //        {
    //            topicCategories = new[] { wikipediaUrl }
    //        }
    //    };

    //    using var httpClient = new HttpClient();
    //    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    //    var json = JsonSerializer.Serialize(patchPayload);
    //    var content = new StringContent(json, Encoding.UTF8, "application/json");

    //    var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"),
    //        $"https://www.googleapis.com/youtube/v3/videos?part=topicDetails")
    //    {
    //        Content = content
    //    };

    //    var response = await httpClient.SendAsync(patchRequest);
    //    var body = await response.Content.ReadAsStringAsync();

    //    if (!response.IsSuccessStatusCode)
    //    {
    //        Console.WriteLine($"PATCH failed: {response.StatusCode}\n{body}");
    //    }
    //    else
    //    {
    //        Console.WriteLine("Gaming-topic satt korrekt.");
    //    }
    //}


    // TODO: Figure out a better way to deduct which stream is which...
    private async void StartStream(object? sender, RoutedEventArgs e)
    {
        StartStreamButton.IsEnabled = false;
        if (SelectedServicesToStream.Count == 0)
            return;

        // Anta vi bara hanterar Youtube här (kan byggas ut senare)
        foreach (var service in SelectedServicesToStream)
        {
            string url;
            string streamKey;

            // Kolla om metadata finns satt (titel eller thumbnail-path)
            if (!string.IsNullOrWhiteSpace(CurrentMetadata?.Title) ||
                !string.IsNullOrWhiteSpace(CurrentMetadata?.ThumbnailPath))
            {
                try
                {
                    if (service.DisplayName.Contains("Youtube", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skapa ny Youtube broadcast med metadata
                        var (newUrl, newKey) = await _streamService.CreateYouTubeBroadcastAsync(CurrentMetadata, this);
                        //SetYouTubeCategoryAndGameAsync(newKey, newUrl, );

                        url = newUrl;
                        streamKey = newKey;
                        // Uppdatera service med nya värden så vi kör rätt stream
                        service.SelectedServer.Url = url;
                        service.StreamKey = streamKey;

                    }
                    if (service.DisplayName.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
                    {
                        var (newUrl, newKey) = await _streamService.CreateTwitchBroadcastAsync(CurrentMetadata);
                        url = newUrl;
                        streamKey = newKey;
                        service.SelectedServer.Url = url;
                        service.StreamKey = streamKey;
                    }
                }
                catch (Exception ex)
                {
                    LogOutput.Text += $"Failed to create YouTube broadcast: {ex.Message}\n";
                    LogOutput.CaretIndex = LogOutput.Text.Length;
                    return;
                }
            }
            else
            {
                // Använd befintliga url/key som redan finns
                url = service.SelectedServer.Url.TrimEnd('/');
                streamKey = service.StreamKey;
            }

            // Bygg ffmpeg argument
            var fullUrl = $"{service.SelectedServer.Url}/{service.StreamKey}";
            var input = RtmpAdress; // exempel, justera efter behov

            var args = new StringBuilder($"-i \"{input}\" ");
            args.Append($"-c:v copy -c:a aac -f flv \"{fullUrl}\" ");

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
                var process = new Process { StartInfo = startInfo };

                ffmpegProcess?.Add(process);
                process.Start();

                // Läs FFmpeg:s standardfelutgång asynkront
                StopStreamButton.IsEnabled = true;
                string? line;
                while ((line = await process.StandardError.ReadLineAsync()) != null)
                {
                    LogOutput.Text += (line + Environment.NewLine);
                    LogOutput.CaretIndex = LogOutput.Text.Length;
                }

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                LogOutput.Text += ($"FFmpeg start failed: {ex.Message}\n");
                LogOutput.CaretIndex = LogOutput.Text.Length;
            }
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
        StartStreamButton.IsEnabled = true;
        StopStreamButton.IsEnabled = false;
    }

    private async void OnUploadThumbnailClicked(object? sender, RoutedEventArgs e)
    {
        if (this.VisualRoot is Window window)
        {

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Thumbnail Image",
                AllowMultiple = false,
                FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
            {
                Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"]
            }
            ]
            };


            var files = await window.StorageProvider.OpenFilePickerAsync(options);

            if (files is not null && files.Count > 0)
            {
                var file = files[0];

                using var stream = await file.OpenReadAsync();
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);

                // show image in UI
                ThumbnailImage.Source = bitmap;
                CurrentMetadata.Thumbnail = bitmap;
                var path = file.Path?.LocalPath ?? "(no local path)";
                LogOutput.Text += ($"Selected thumbnail: {path}");
                LogOutput.CaretIndex = LogOutput.Text.Length;

                StatusTextBlock.Foreground = Avalonia.Media.Brushes.Green;
                StatusTextBlock.Text = "Thumbnail loaded successfully.";
                // set path to metadataobjekt
                CurrentMetadata.ThumbnailPath = path;
            }
            else
            {
                StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
                StatusTextBlock.Text = "No file selected.";
            }
        }
        else
        {
            throw new Exception("Our parameter window was null");
        }
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
    private void OnUpdateMetadataClicked(object? sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
            StatusTextBlock.Text = "Please enter a stream title.";
            return;
        }

        //TitleOfStream.Text = $"Stream Title: {title}";

        // set title of MetaData 
        CurrentMetadata.Title = title;

        // UI indicator
        StatusTextBlock.Foreground = Avalonia.Media.Brushes.Green;
        StatusTextBlock.Text = "Metadata updated successfully!";
    }

    private async void OnLoginWithGoogleClicked(object? sender, RoutedEventArgs e)
    {
        LoginStatusText.Text = "Loggar in...";

        var userName = await _centralAuthService.LoginWithYoutube(this);
        if (userName != null)
        {
            LoginStatusText.Text = $"✅ Inloggad som {userName}";
        }
        else
        {
            LoginStatusText.Text = "Inloggning misslyckades";
        }
    }

    private async void LoginWithTwitch(object? sender, RoutedEventArgs e)
    {
        TwitchLogin.Text = await _centralAuthService.LoginWithTwitch();
    }

    /// <summary>
    /// Debug only
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void ClearGoogleOAuthTokens(object? sender, RoutedEventArgs e)
    {
        string[] possiblePaths =
        [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".credentials"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google.Apis.Auth")
    ];

        bool foundAndDeleted = false;

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                    Console.WriteLine($"OAuth tokens cleared from: {path}");
                    foundAndDeleted = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to clear OAuth tokens at {path}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"No OAuth tokens found at: {path}");
            }
        }

        if (!foundAndDeleted)
        {
            Console.WriteLine("No OAuth token directories found to clear.");
        }
    }

    private async void LoginWithKick(object? sender, RoutedEventArgs e)
    {
        var Result = await _centralAuthService.LoginWithKick();
        KickLogin.Text = Result;
    }
}