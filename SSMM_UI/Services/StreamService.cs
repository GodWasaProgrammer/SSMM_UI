using Google.Apis.YouTube.v3;
using SSMM_UI.MetaData;
using SSMM_UI.Puppeteering;
using SSMM_UI.RTMP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class StreamService
{
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

    private readonly List<Process>? ffmpegProcess = [];
    private readonly BroadCastService _broadCastService;
    private readonly ILogService _logger;
    private readonly PuppetMaster puppetMaster;
    public List<StreamProcessInfo> ProcessInfos { get; private set; } = [];
    public StreamService(ILogService logger, BroadCastService broadCastService, PuppetMaster puppeteer)
    {
        RTMPServer.StartSrv();
        _logger = logger;
        _broadCastService = broadCastService;
        puppetMaster = puppeteer;
    }

    // TODO: needs to indicate success
    public async Task StartStream(StreamMetadata metadata, ObservableCollection<SelectedService> SelectedServicesToStream)
    {
        if (SelectedServicesToStream.Count == 0)
        {
            return;
        }

        string YTbroadcastId = string.Empty;
        YouTubeService? _ytService = null;
        foreach (var service in SelectedServicesToStream)
        {
            // Kolla om metadata finns satt (titel eller thumbnail-path)
            if (metadata != null || !string.IsNullOrWhiteSpace(metadata?.Title) ||
                !string.IsNullOrWhiteSpace(metadata?.ThumbnailPath) || service.SelectedServer != null)
            {
                try
                {
                    if (service.DisplayName.Contains("Youtube", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skapa ny Youtube broadcast med metadata
                        if (metadata != null)
                        {

                            var (newUrl, newKey, id, ytservice) = await _broadCastService.CreateYouTubeBroadcastAsync(metadata);
                            YTbroadcastId = id;
                            _ytService = ytservice;
                            // Uppdatera service med nya värden så vi kör rätt stream
                            if (service.SelectedServer != null)
                            {
                                service.SelectedServer.Url = newUrl;
                                service.StreamKey = newKey;
                            }
                        }

                    }
                    if (service.DisplayName.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
                    {
                        if (metadata != null)
                        {
                            var (newUrl, newKey) = await _broadCastService.CreateTwitchBroadcastAsync(metadata);

                            if (newUrl != null && newKey != null)
                            {
                                if (service.SelectedServer != null)
                                    service.SelectedServer.Url = newUrl;
                                service.StreamKey = newKey;
                            }
                            else
                            {
                                throw new Exception($"CreateTwitchBroadcast returned a null value in either{newUrl} or {newKey}");
                            }
                        }
                    }
                    if (service.DisplayName.Contains("Kick", StringComparison.OrdinalIgnoreCase))
                    {
                        if (metadata != null)
                        {
                            await _broadCastService.CreateKickBroadcastAsync(metadata);
                        }
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
            if (service.SelectedServer != null)
            {

                if (service.SelectedServer.Url.StartsWith("rtmps://"))
                {
                    // För RTMP:S, använd vanlig sammansättning men se till att port 443 används

                    fullUrl = $"{service.SelectedServer.Url}:443/app/{service.StreamKey}";

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

                if (service.ServiceGroup != null)
                {

                    if (service.ServiceGroup.RecommendedSettings != null)
                    {
                        var recommended = service.ServiceGroup.RecommendedSettings;


                        //// Video codec
                        //if (recommended?.SupportedVideoCodes?.Length > 0)
                        //{
                        //    args.Append($"-c:v {recommended.SupportedVideoCodes[0]} ");
                        //}
                        //else
                        //{
                        args.Append("-c:v copy "); // fallback
                                                   //}

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
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Vänta tills YouTube har fått RTMP-signal
                                for (int i = 0; i < 20; i++)
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(5));

                                    var listRequest = _ytService.LiveBroadcasts.List("id,status");
                                    listRequest.Id = YTbroadcastId;
                                    var listResponse = await listRequest.ExecuteAsync();

                                    var broadcast = listResponse.Items.FirstOrDefault();
                                    var state = broadcast?.Status?.LifeCycleStatus;

                                    _logger.Log($"[YouTube] Broadcast lifecycle: {state}");

                                    if (state == "ready")
                                    {
                                        await Task.Delay(TimeSpan.FromSeconds(10));
                                        var transitionRequest = _ytService.LiveBroadcasts.Transition(
                                            LiveBroadcastsResource.TransitionRequest.BroadcastStatusEnum.Live,
                                            YTbroadcastId,
                                            "status"
                                        );

                                        var response = await transitionRequest.ExecuteAsync();
                                        _logger.Log($"YouTube broadcast transitioned to LIVE: {response.Snippet.Title}");
                                        return;
                                    }
                                }

                                _logger.Log("YouTube broadcast never reached 'ready' state.");
                            }
                            catch (Exception ex)
                            {
                                _logger.Log($"Failed to auto-start YouTube broadcast: {ex.Message}");
                            }
                        });
                    }
                }
            }
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
