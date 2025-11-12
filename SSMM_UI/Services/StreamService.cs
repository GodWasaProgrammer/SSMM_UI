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
    public async Task StartStream(StreamMetadata metadata, ObservableCollection<SelectedService> SelectedServicesToStream, Action<bool>? onYouTubeStatusChanged = null)
    {
        if (SelectedServicesToStream.Count == 0)
        {
            return;
        }

        string YTbroadcastId = string.Empty;
        YouTubeService? _ytService = null;
        string streamId = string.Empty;
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

                            var (newUrl, newKey, id, ytservice, streamid) = await _broadCastService.CreateYouTubeBroadcastAsync(metadata);
                            YTbroadcastId = id;
                            streamId = streamid;
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
                        if (service.DisplayName.Contains("Youtube", StringComparison.OrdinalIgnoreCase))
                        {

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    bool streamActive = false;
                                    bool broadcastReady = false;

                                    var startTime = DateTime.UtcNow;
                                    var timeout = TimeSpan.FromMinutes(5.5);
                                    _logger.Log("Waiting one minute before polling youtube to go LIVE");
                                    await Task.Delay(TimeSpan.FromMinutes(1));

                                    while (DateTime.UtcNow - startTime < timeout)
                                    {
                                        await Task.Delay(TimeSpan.FromSeconds(15));

                                        // 🔹 1. Kolla stream status
                                        if (!streamActive)
                                        {
                                            if (_ytService != null)
                                            {
                                                var streamListReq = _ytService.LiveStreams.List("status");
                                                streamListReq.Id = streamId;
                                                var streamListResp = await streamListReq.ExecuteAsync();
                                                var streamStatus = streamListResp.Items.FirstOrDefault()?.Status?.StreamStatus;

                                                _logger.Log($"[YouTube] Stream status: {streamStatus}");

                                                if (streamStatus == "active")
                                                {
                                                    streamActive = true;
                                                    _logger.Log("[YouTube] RTMP-stream is active!");
                                                }
                                                else
                                                {
                                                    continue; // vänta vidare tills RTMP är aktiv
                                                }
                                            }
                                        }

                                        // 🔹 2. Kolla broadcast lifecycle
                                        if (!broadcastReady)
                                        {
                                            if (_ytService != null)
                                            {
                                                var broadcastReq = _ytService.LiveBroadcasts.List("status");
                                                broadcastReq.Id = YTbroadcastId;
                                                var broadcastResp = await broadcastReq.ExecuteAsync();
                                                var lifecycle = broadcastResp.Items.FirstOrDefault()?.Status?.LifeCycleStatus;

                                                _logger.Log($"[YouTube] Broadcast lifecycle: {lifecycle}");

                                                if (lifecycle == "ready")
                                                {
                                                    broadcastReady = true;
                                                    _logger.Log("[YouTube] Broadcast is ready for transition!");
                                                }
                                                else
                                                {
                                                    continue;
                                                }
                                            }
                                        }

                                        // 🔹 3. Försök transitionera till LIVE
                                        if (streamActive && broadcastReady)
                                        {
                                            try
                                            {
                                                _logger.Log("[YouTube] Attempting to transition broadcast to LIVE...");

                                                if (_ytService != null)
                                                {

                                                    var transitionReq = _ytService.LiveBroadcasts.Transition(
                                                        LiveBroadcastsResource.TransitionRequest.BroadcastStatusEnum.Live,
                                                        YTbroadcastId,
                                                        "snippet,status"
                                                    );

                                                    var resp = await transitionReq.ExecuteAsync();


                                                    _logger.Log($"✅ YouTube broadcast transitioned to LIVE: {resp.Snippet.Title}");
                                                    onYouTubeStatusChanged?.Invoke(true);

                                                    return; // färdigt!
                                                }
                                            }
                                            catch (Google.GoogleApiException gex)
                                            {
                                                var reason = gex.Error?.Errors?.FirstOrDefault()?.Reason ?? gex.Message;
                                                _logger.Log($"⚠️ Transition failed ({reason}). Will retry...");

                                                // Vänta 10 sekunder och försök igen under timeoutperioden
                                                await Task.Delay(TimeSpan.FromSeconds(10));
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.Log($"❌ Unexpected error during transition: {ex.Message}");
                                                await Task.Delay(TimeSpan.FromSeconds(10));
                                                onYouTubeStatusChanged?.Invoke(false);
                                            }
                                        }
                                    }

                                    _logger.Log("❌ Timed out waiting for YouTube broadcast to go LIVE.");
                                    onYouTubeStatusChanged?.Invoke(false);
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