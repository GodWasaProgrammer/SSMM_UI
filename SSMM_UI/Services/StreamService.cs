using FFmpeg.AutoGen;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Puppeteering;
using SSMM_UI.RTMP;
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

namespace SSMM_UI.Services;

public class StreamService : IDisposable
{
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";
    private const string TwitchAdress = "rtmp://live.twitch.tv/app";
    public event Action<bool>? ServerStatusChanged;
    public event Action<bool>? StreamStatusChanged;

    private RTMPServer Server { get; set; } = new();
    private readonly List<Process>? ffmpegProcess = [];
    private readonly CancellationTokenSource _cts = new();
    private YouTubeService? _youTubeService;
    private CentralAuthService _authService;
    private ILogService _logger;
    private MetaDataService MDService;
    public List<StreamProcessInfo> ProcessInfos { get; private set; } = new List<StreamProcessInfo>();
    public StreamService(CentralAuthService authService, ILogService logger, MetaDataService MdService)
    {
        Server.StartSrv();
        _authService = authService;
        _logger = logger;
        MDService = MdService;
    }

    public void CreateYTService(YouTubeService YTService)
    {
        _youTubeService = YTService;
    }

    public StreamInfo? StreamInfo { get; set; }
    public async Task<(string rtmpUrl, string streamKey)> CreateYouTubeBroadcastAsync(StreamMetadata metadata)
    {

        try
        {
            var info = await ProbeStreamAsync(RtmpAdress);
            if (info is not null)
            {
                StreamInfo = info;
            }
            else
            {
                _logger.Log("stream failed to start, there was missing info from ffprobe");
                throw new Exception("ffprobe failed to find any stream");
            }

        }
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }
        if (_youTubeService is not null && StreamInfo is not null)
        {
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

                var broadcastInsert = _youTubeService.LiveBroadcasts.Insert(liveBroadcast, "snippet,status");
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

                var streamInsert = _youTubeService.LiveStreams.Insert(liveStream, "snippet,cdn");
                var insertedStream = await streamInsert.ExecuteAsync();

                // 3. Koppla stream till broadcast
                var bindRequest = _youTubeService.LiveBroadcasts.Bind(insertedBroadcast.Id, "id,contentDetails");
                bindRequest.StreamId = insertedStream.Id;
                await bindRequest.ExecuteAsync();

                // 4. Upload thumbnail om vald
                if (!string.IsNullOrWhiteSpace(metadata.ThumbnailPath))
                {
                    using var fs = new FileStream(metadata.ThumbnailPath, FileMode.Open, FileAccess.Read);
                    var thumbnailRequest = _youTubeService.Thumbnails.Set(insertedBroadcast.Id, fs, "image/jpeg");
                    await thumbnailRequest.UploadAsync();
                }

                int category;
                if (metadata != null)
                {
                    if (metadata.YouTubeCategory != null)
                    {
                        if (metadata.YouTubeCategory.Id != null)
                        {
                            var success = int.TryParse(metadata.YouTubeCategory.Id, out category);
                            if (success)
                            {
                                await MDService.SetTitleAndCategoryYoutubeAsync(insertedBroadcast.Id, null, category);

                            }
                        }
                    }
                }

                await PuppetMaster.ChangeGameTitleYoutube(insertedBroadcast.Id, "Hearts Of Iron IV");

                // 5. Returnera RTMP-url + streamkey
                var ingestionInfo = insertedStream.Cdn.IngestionInfo;
                return (ingestionInfo.IngestionAddress, ingestionInfo.StreamName);
            }
            catch (Google.GoogleApiException ex)
            {
                _logger.Log("YouTube API error:");
                _logger.Log($"Message: {ex.Message}");
                _logger.Log($"Details: {ex.Error?.Errors?.FirstOrDefault()?.Message}");
                _logger.Log($"Reason: {ex.Error?.Errors?.FirstOrDefault()?.Reason}");
                _logger.Log($"Domain: {ex.Error?.Errors?.FirstOrDefault()?.Domain}");
                throw;
            }
        }
        else
        {
            if (_youTubeService is null)
                throw new Exception("CentralAuthService.YTService Was null");
            if (StreamInfo is null)
                throw new Exception("Streaminfo failed to fetch data");
        }
        return (string.Empty, string.Empty);
    }

    public async Task<(string rtmpUrl, string? streamKey)> CreateTwitchBroadcastAsync(StreamMetadata metadata)
    {
        var accessToken = _authService.TwitchService.AuthResult.AccessToken;
        var ClientId = _authService.TwitchService._clientId;
        var userId = _authService.TwitchService.AuthResult.UserId;
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Client-Id", ClientId);

        // Title and game should be set after this
        await MDService.SetTwitchTitleAndCategory(metadata.Title, metadata.TwitchCategory.Name);


        // Twitch RTMP-info är statisk (RTMP URL och stream key)
        var streamKeyResponse = await httpClient.GetAsync($"https://api.twitch.tv/helix/streams/key?broadcaster_id={userId}");
        streamKeyResponse.EnsureSuccessStatusCode();

        var json = await streamKeyResponse.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var key = doc.RootElement.GetProperty("data")[0].GetProperty("stream_key").GetString();

        return (TwitchAdress, key);
    }
    public async Task CreateKickBroadcastAsync(StreamMetadata metadata)
    {
        //TODO: There needs to be automation with for example puppeteer here to control the stream name, title etc as the Kick API does not support setting this programmatically.

        // kick keys remain the same unless reset

        await PuppetMaster.SetKickGameTitle(StreamTitle: metadata.Title);
    }
    public async Task<(string rtmpUrl, string? streamKey)> CreateTrovoBroadcastAsync(StreamMetadata metadata)
    {
        try
        {
            using var httpClient = new HttpClient();

            // 1. Sätt headers för autentisering
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            //httpClient.DefaultRequestHeaders.Add("Client-ID", _trovoClientId);
            //httpClient.DefaultRequestHeaders.Add("Authorization", $"OAuth {_trovoAccessToken}");

            // 2. Skapa PATCH-data (Trovo förväntar sig JSON)
            var patchData = new
            {
                title = metadata.Title,
                //  category_id = metadata.CategoryId, // Kräver Trovo-specifikt kategori-ID
                language = "en",                   // Exempel: "sv" för svenska
                is_live = true                     // Markera som live-sändning
            };

            var jsonContent = JsonSerializer.Serialize(patchData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // 3. Skicka PATCH-förfrågan
            var response = await httpClient.PatchAsync(
                "https://open-api.trovo.live/openplatform/channels/update",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Trovo API error: {response.StatusCode} - {errorContent}");
            }

            // 4. Returnera RTMP-uppgifter
            const string trovoRtmpUrl = "rtmp://live.trovo.live/live/";
            return (trovoRtmpUrl, "_trovoStreamKey"); // Se anteckning nedan om streamKey
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to create Trovo broadcast: {ex.Message}");
            throw;
        }
    }
    public async Task<(string rtmpUrl, string? streamkey)> CreateFacebookBroadcastAsync(StreamMetadata metadata)
    {
        // TODO: implement
        return ("", "");
    }
    public async Task<StreamInfo?> ProbeStreamAsync(string rtmpUrl)
    {
        var path = "Dependencies/ffprobe";
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"-v error -select_streams v:0 -read_intervals %+#5 -show_entries stream=width,height,r_frame_rate " +
                            $"-of default=noprint_wrappers=1:nokey=1 \"{rtmpUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Läs output asynkront med timeout
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(outputTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill();
                _logger.Log("ffprobe process killed due to timeout.");
                return null; // ✅ Returnerar null vid timeout
            }

            var output = await outputTask;
            await Task.Run(() => process.WaitForExit(1000));

            var lines = output.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3)
            {
                return null; // ✅ Returnerar null vid för få rader
            }

            int width = int.Parse(lines[0]);
            int height = int.Parse(lines[1]);
            string rawFramerate = lines[2];

            var parts = rawFramerate.Split('/');
            double fps = parts.Length == 2 && double.TryParse(parts[0], out var num) && double.TryParse(parts[1], out var den) && den != 0
                ? num / den
                : 30.0;

            string frameRateLabel = fps >= 59 ? "60fps" : (fps >= 29 ? "30fps" : "24fps");

            return new StreamInfo // ✅ Returnerar StreamInfo vid success
            {
                Width = width,
                Height = height,
                FrameRate = frameRateLabel
            };
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to probe RTMP stream: {ex.Message}");
            return null; // ✅ Returnerar null vid exception
        }

        // ✅ Alla kodvägar returnerar nu ett värde!
    }

    public void Dispose() => _cts.Cancel();

    // TODO: to reflect user settings this needs to be called individually
    public void StartPolling()
    {
        _ = StartStreamStatusPolling();
        _ = StartServerStatusPolling();
    }

    private async Task StartStreamStatusPolling()
    {
        while (!_cts.IsCancellationRequested)
        {
            var isAlive = await Task.Run(() => CheckStreamIsAlive(RtmpAdress));
            StreamStatusChanged?.Invoke(isAlive);
        }
    }
    private static async Task<bool> IsRtmpApiResponding()
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
    private async Task StartServerStatusPolling()
    {
        while (!_cts.IsCancellationRequested)
        {
            bool isResponding = await IsRtmpApiResponding(); // Använd await istället för .Result

            ServerStatusChanged?.Invoke(isResponding);

            await Task.Delay(5000); // 5 sekunders delay
        }
    }

    // TODO: needs to indicate success
    public async Task StartStream(StreamMetadata metadata, ObservableCollection<SelectedService> SelectedServicesToStream)
    {
        if (SelectedServicesToStream.Count == 0)
        {
            return;
        }

        foreach (var service in SelectedServicesToStream)
        {
            // Kolla om metadata finns satt (titel eller thumbnail-path)
            if (!string.IsNullOrWhiteSpace(metadata?.Title) ||
                !string.IsNullOrWhiteSpace(metadata?.ThumbnailPath))
            {
                try
                {
                    if (service.DisplayName.Contains("Youtube", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skapa ny Youtube broadcast med metadata
                        var (newUrl, newKey) = await CreateYouTubeBroadcastAsync(metadata);

                        // Uppdatera service med nya värden så vi kör rätt stream
                        service.SelectedServer.Url = newUrl;
                        service.StreamKey = newKey;
                    }
                    if (service.DisplayName.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
                    {
                        var (newUrl, newKey) = await CreateTwitchBroadcastAsync(metadata);

                        if (newUrl != null && newKey != null)
                        {
                            service.SelectedServer.Url = newUrl;
                            service.StreamKey = newKey;
                        }
                        else
                        {
                            throw new Exception($"CreateTwitchBroadcast returned a null value in either{newUrl} or {newKey}");
                        }
                    }
                    if (service.DisplayName.Contains("Kick", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateKickBroadcastAsync(metadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to create YouTube broadcast: {ex.Message}\n");
                    return;
                }
            }

            var path = "Dependencies/ffmpeg";

            // Build FFMpeg Args

            string fullUrl;
            if (service.SelectedServer.Url.StartsWith("rtmps://"))
            {
                // För RTMP:S, använd vanlig sammansättning men se till att port 443 används

                fullUrl = $"{service.SelectedServer.Url}:443/{service.StreamKey}";

            }
            else
            {
                // För vanlig RTMP
                fullUrl = $"{service.SelectedServer.Url}/{service.StreamKey}";
            }

            // endpoint to send output through
            //var fullUrl = $"{service.SelectedServer.Url}/{service.StreamKey}";

            // our internal rtmp feed
            var input = RtmpAdress;

            // create our stringbuilder
            var args = new StringBuilder($"-i \"{input}\" ");

            var recommended = service.ServiceGroup.RecommendedSettings;

            // Video codec
            if (recommended?.SupportedVideoCodes?.Length > 0)
            {
                args.Append($"-c:v {recommended.SupportedVideoCodes[0]} ");
            }
            else
            {
                args.Append("-c:v copy "); // fallback
            }

            // Video bitrate
            if (recommended?.MaxVideoBitRate != null)
            {
                args.Append($"-b:v {recommended.MaxVideoBitRate}k ");
            }

            // Keyint (nyckelframe interval)
            if (recommended?.KeyInt != null)
            {
                args.Append($"-g {recommended.KeyInt} ");
            }

            // Audio bitrate
            if (recommended?.MaxAudioBitRate != null)
            {
                args.Append($"-b:a {recommended.MaxAudioBitRate}k ");
            }

            args.Append($"-f flv \"{fullUrl}\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = args.ToString(),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var process = new Process { StartInfo = startInfo };
                var processinfo = new StreamProcessInfo { Header = service.DisplayName, Process = process };
                ProcessInfos.Add(processinfo);
                ffmpegProcess?.Add(process);
                process.Start();

            }
            catch (Exception ex)
            {
                _logger.Log($"FFmpeg start failed: {ex.Message}\n");
            }
        }
        //ReadOutPut();
    }

    public void ReadOutPut()
    {
        foreach (var process in ffmpegProcess)
        {
            // Läs standardfel asynkront utan att blockera loopen
            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync()) != null)
                {
                    _logger.Log(line + Environment.NewLine);
                }
            });
        }
    }

    public void StopStreams()
    {
        if (ffmpegProcess != null)
        {
            foreach (var process in ffmpegProcess)
            {
                process.Kill();
            }
        }
    }
}
