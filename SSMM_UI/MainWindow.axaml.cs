using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using FFmpeg.AutoGen;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Dialogs;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.Oauth.Twitch;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace SSMM_UI;

public partial class MainWindow : Window
{
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];

    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];

    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

    public StreamInfo? StreamInfo { get; set; }

    public RTMPServer Server { get; set; } = new();

    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

    private YouTubeService _youtubeService = new();
    private TwitchDCAuthService _twitchService;
    private bool isReceivingStream = false;
    private Task _serverTask;
    private readonly List<Process>? ffmpegProcess = [];
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadRtmpServersFromServicesJson("services.json");
        StartStreamStatusPolling();
        StartServerStatusPolling();
        if (!Design.IsDesignMode)
            RtmpIncoming.Play(RtmpAdress);
        RunRTMPServer();
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

    private void LoadRtmpServersFromServicesJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var services = doc.RootElement.GetProperty("services");

        foreach (var service in services.EnumerateArray())
        {
            if (service.TryGetProperty("protocol", out var proto) && !proto.GetString()!.Contains("rtmp", StringComparison.CurrentCultureIgnoreCase))
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

    private const string TwitchAdress = "rtmp://live.twitch.tv/app";
    public async Task<(string rtmpUrl, string streamKey)> CreateTwitchBroadcastAsync(StreamMetadata metadata)
    {
        var accessToken = _twitchService.AuthResult.AccessToken;
        var ClientId = _twitchService._clientId;
        var userId = _twitchService.AuthResult.UserId;
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Client-Id", ClientId);

        var content = new StringContent(JsonSerializer.Serialize(new
        {
            title = metadata.Title,
            //game_id = metadata.GameId // Valfritt: kräver att du hämtat game_id först
        }), Encoding.UTF8, "application/json");

        var response = await httpClient.PatchAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={userId}", content);
        response.EnsureSuccessStatusCode();

        // Twitch RTMP-info är statisk (RTMP URL och stream key)
        var streamKeyResponse = await httpClient.GetAsync($"https://api.twitch.tv/helix/streams/key?broadcaster_id={userId}");
        streamKeyResponse.EnsureSuccessStatusCode();

        var json = await streamKeyResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var key = doc.RootElement.GetProperty("data")[0].GetProperty("stream_key").GetString();

        return (TwitchAdress, key);
    }

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
                        var (newUrl, newKey) = await CreateYouTubeBroadcastAsync(CurrentMetadata);
                        // Skapa ny Youtube broadcast med metadata
                        url = newUrl;
                        streamKey = newKey;
                        // Uppdatera service med nya värden så vi kör rätt stream
                        service.SelectedServer.Url = url;
                        service.StreamKey = streamKey;

                    }
                    if (service.DisplayName.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
                    {
                        var (newUrl, newKey) = await CreateTwitchBroadcastAsync(CurrentMetadata);
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
    public static StreamInfo? ProbeStream(string rtmpUrl)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -select_streams v:0 -read_intervals %+#5 -show_entries stream=width,height,r_frame_rate " +
                            $"-of default=noprint_wrappers=1:nokey=1 \"{rtmpUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000); // max 5 sekunder

            if (!process.HasExited)
            {
                process.Kill();
                Console.WriteLine("ffprobe process killed due to timeout.");
                return null;
            }

            var lines = output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) return null;

            int width = int.Parse(lines[0]);
            int height = int.Parse(lines[1]);
            string rawFramerate = lines[2]; // ex: "60/1"

            var parts = rawFramerate.Split('/');
            double fps = parts.Length == 2 && double.TryParse(parts[0], out var num) && double.TryParse(parts[1], out var den) && den != 0
                ? num / den
                : 30.0;

            string frameRateLabel = fps >= 59 ? "60fps" : (fps >= 29 ? "30fps" : "24fps");
            return new StreamInfo
            {
                Width = width,
                Height = height,
                FrameRate = frameRateLabel
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to probe RTMP stream: {ex.Message}");
            return null;
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

    private async Task<(string rtmpUrl, string streamKey)> CreateYouTubeBroadcastAsync(StreamMetadata metadata)
    {
        var youtubeService = _youtubeService;
        if (StreamInfo == null)
        {
            var info = await Task.Run(() => ProbeStream(RtmpAdress));
            if (info is not null)
                StreamInfo = info;
            else
            {
                LogOutput.Text += "stream failed to start, there was missing info from ffprobe";
                LogOutput.CaretIndex = LogOutput.Text.Length;
            }
        }
        try
        {
            // 1. Skapa LiveBroadcast
            var broadcastSnippet = new LiveBroadcastSnippet
            {
                Title = metadata.Title,
                ScheduledStartTimeDateTimeOffset = DateTime.UtcNow.AddMinutes(1)
            };

            var broadcastStatus = new LiveBroadcastStatus
            {
                PrivacyStatus = "private"
            };

            var liveBroadcast = new LiveBroadcast
            {
                Kind = "youtube#liveBroadcast",
                Snippet = broadcastSnippet,
                Status = broadcastStatus
            };

            var broadcastInsert = youtubeService.LiveBroadcasts.Insert(liveBroadcast, "snippet,status");
            var insertedBroadcast = await broadcastInsert.ExecuteAsync();

            // 2. Skapa LiveStream
            var streamSnippet = new LiveStreamSnippet
            {
                Title = metadata.Title + " Stream"
            };

            CdnSettings cdn = new();
            if (StreamInfo != null)
            {
                cdn = new CdnSettings
                {
                    //Format = "1080p", // Testa med 1080p, fungerar på de flesta konton
                    IngestionType = "rtmp",
                    FrameRate = StreamInfo.FrameRate,
                    Resolution = StreamInfo.Resolution
                };
            }

            var liveStream = new LiveStream
            {
                Kind = "youtube#liveStream",
                Snippet = streamSnippet,
                Cdn = cdn
            };

            var streamInsert = youtubeService.LiveStreams.Insert(liveStream, "snippet,cdn");
            var insertedStream = await streamInsert.ExecuteAsync();

            // 3. Koppla stream till broadcast
            var bindRequest = youtubeService.LiveBroadcasts.Bind(insertedBroadcast.Id, "id,contentDetails");
            bindRequest.StreamId = insertedStream.Id;
            await bindRequest.ExecuteAsync();

            // 4. Upload thumbnail om vald
            if (!string.IsNullOrWhiteSpace(metadata.ThumbnailPath))
            {
                using var fs = new FileStream(metadata.ThumbnailPath, FileMode.Open, FileAccess.Read);
                var thumbnailRequest = youtubeService.Thumbnails.Set(insertedBroadcast.Id, fs, "image/jpeg");
                await thumbnailRequest.UploadAsync();
            }

            // 5. Returnera RTMP-url + streamkey
            var ingestionInfo = insertedStream.Cdn.IngestionInfo;
            return (ingestionInfo.IngestionAddress, ingestionInfo.StreamName);
        }
        catch (Google.GoogleApiException ex)
        {
            Console.WriteLine("YouTube API error:");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Details: {ex.Error?.Errors?.FirstOrDefault()?.Message}");
            Console.WriteLine($"Reason: {ex.Error?.Errors?.FirstOrDefault()?.Reason}");
            Console.WriteLine($"Domain: {ex.Error?.Errors?.FirstOrDefault()?.Domain}");
            throw;
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

        var userInfo = await AuthenticateWithGoogleAsync(this);
        if (userInfo != null)
        {
            LoginStatusText.Text = $"✅ Inloggad som {userInfo.Name}";
        }
        else
        {
            LoginStatusText.Text = "Inloggning misslyckades";
        }
    }

    private async void LoginWithTwitch(object? sender, RoutedEventArgs e)
    {
        var clId = Environment.GetEnvironmentVariable("TwitchDCFClient");
        if (string.IsNullOrEmpty(clId))
        {
            throw new Exception("ClId was missing");
        }
        var scopes = new[]
        {
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        };
        _twitchService = new TwitchDCAuthService(new HttpClient(), clId, scopes);

        var IsTokenValid = _twitchService.TryLoadValidOrRefreshTokenAsync();

        if (IsTokenValid.Result != null)
        {
            if (IsTokenValid.Result != null)
            {
                TwitchLogin.Text = ($"✅ Inloggad som: {IsTokenValid.Result.UserName}");
            }
        }
        else
        {
            var device = await _twitchService.StartDeviceCodeFlowAsync();

            Process.Start(new ProcessStartInfo
            {
                FileName = device.VerificationUri,
                UseShellExecute = true
            });
            // Visa UI eller vänta medan användaren godkänner
            var token = await _twitchService.PollForTokenAsync(device.DeviceCode, device.Interval);

            if (token != null)
            {
                TwitchLogin.Text = ($"✅ Inloggad som: {token.UserName}");
            }
            else
            {
                Console.WriteLine("Timeout - användaren loggade inte in.");
            }
        }
    }

    public async Task<Userinfo?> AuthenticateWithGoogleAsync(Window parentWindow)
    {
#if DEBUG
        var clID = Environment.GetEnvironmentVariable("SSMM_ClientID");
        var clSecret = Environment.GetEnvironmentVariable("SSMM_ClientSecret");
#endif
        var clientSecrets = new ClientSecrets
        {
            ClientId = Environment.GetEnvironmentVariable("SSMM_ClientID"),
            ClientSecret = Environment.GetEnvironmentVariable("SSMM_ClientSecret")
        };

        string[] scopes = [
        Oauth2Service.Scope.UserinfoProfile,
        Oauth2Service.Scope.UserinfoEmail,
        YouTubeService.Scope.Youtube,
        YouTubeService.Scope.YoutubeForceSsl
    ];

        try
        {
            // Kör OAuth-flödet, med lokal webbläsare och redirect
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                scopes,
                "user", // användar-ID för att spara token lokalt
                CancellationToken.None);

            // Skapa tjänst för att hämta info om användaren
            var oauth2Service = new Oauth2Service(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "SSMM_UI"
            });

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "SSMM"
            });

            _youtubeService = youtubeService;

            // Hämta användarprofil
            var userInfo = await oauth2Service.Userinfo.Get().ExecuteAsync();
            return userInfo;
        }
        catch (Exception ex)
        {
            await MessageBox.Show(parentWindow, $"OAuth failed: {ex.Message}");
            return null;
        }
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
        var authManager = new KickOAuthService();
        // Ange vilka scopes du behöver
        var requestedScopes = new[] {
                KickOAuthService.Scopes.ChannelWrite,
                KickOAuthService.Scopes.ChannelRead,
                KickOAuthService.Scopes.UserRead
            };
        //var result = await authManager.AuthenticateUserAsync(requestedScopes);
        var result = await authManager.AuthenticateOrRefreshAsync(requestedScopes);
        KickLogin.Text = "Logging in...";

        if (result != null)
        {
            KickLogin.Text = ($"✅ Inloggad som {result.Username}");
        }
        else
        {
            KickLogin.Text = ("❌ Inloggning misslyckades.");
        }
    }
}