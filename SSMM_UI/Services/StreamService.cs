using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class StreamService
{
    private CentralAuthService _centralAuthService;
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";
    private const string TwitchAdress = "rtmp://live.twitch.tv/app";
    public StreamService(CentralAuthService _AuthService)
    {
        _centralAuthService = _AuthService;
    }
    public StreamInfo? StreamInfo { get; set; }
    public async Task<(string rtmpUrl, string streamKey)> CreateYouTubeBroadcastAsync(StreamMetadata metadata, MainWindow window)
    {
        if (_centralAuthService.YTService is not null)
        {

            if (StreamInfo == null)
            {
                var info = await Task.Run(() => ProbeStream(RtmpAdress));
                if (info is not null)
                    StreamInfo = info;
                else
                {
                    window.LogOutput.Text += "stream failed to start, there was missing info from ffprobe";
                    window.LogOutput.CaretIndex = window.LogOutput.Text.Length;
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

                var broadcastInsert = _centralAuthService.YTService.LiveBroadcasts.Insert(liveBroadcast, "snippet,status");
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

                var streamInsert = _centralAuthService.YTService.LiveStreams.Insert(liveStream, "snippet,cdn");
                var insertedStream = await streamInsert.ExecuteAsync();

                // 3. Koppla stream till broadcast
                var bindRequest = _centralAuthService.YTService.LiveBroadcasts.Bind(insertedBroadcast.Id, "id,contentDetails");
                bindRequest.StreamId = insertedStream.Id;
                await bindRequest.ExecuteAsync();

                // 4. Upload thumbnail om vald
                if (!string.IsNullOrWhiteSpace(metadata.ThumbnailPath))
                {
                    using var fs = new FileStream(metadata.ThumbnailPath, FileMode.Open, FileAccess.Read);
                    var thumbnailRequest = _centralAuthService.YTService.Thumbnails.Set(insertedBroadcast.Id, fs, "image/jpeg");
                    await thumbnailRequest.UploadAsync();
                }

                //await SetYouTubeCategoryAndGameAsync(insertedBroadcast.Id, "https://en.wikipedia.org/wiki/Hearts_of_Iron_IV", YTAccessToken);

                //await LoginCapture.RunAsync(); // Första gången
                //await YouTubeStudioAutomation.RunAsync(insertedBroadcast.Id); // Efteråt

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
        else
        {
            throw new Exception("CentralAuthService.YTService Was null");
        }
    }

    public async Task<(string rtmpUrl, string streamKey)> CreateTwitchBroadcastAsync(StreamMetadata metadata)
    {
        var accessToken = _centralAuthService.TwitchService.AuthResult.AccessToken;
        var ClientId = _centralAuthService.TwitchService._clientId;
        var userId = _centralAuthService.TwitchService.AuthResult.UserId;
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
}
