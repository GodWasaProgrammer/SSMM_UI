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
    private readonly List<Process>? pauseProcesses = [];
    private readonly BroadCastService _broadCastService;
    private readonly ILogService _logger;
    private readonly PauseInterjectService _pauseInterjectService;
    public List<StreamProcessInfo> ProcessInfos { get; private set; } = [];
    public StreamService(ILogService logger, BroadCastService broadCastService, PauseInterjectService pauseInterjectService)
    {
        RTMPServer.StartSrv();
        _logger = logger;
        _broadCastService = broadCastService;
        _pauseInterjectService = pauseInterjectService;
    }

    // TODO: needs to indicate success
    public async Task StartStream(StreamMetadata? metadata, ObservableCollection<SelectedService> SelectedServicesToStream, Action<bool>? onYouTubeStatusChanged = null)
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
                if (metadata != null)
                {
                    ApplyGenericMetadataArgs(args, metadata, service);
                }

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

        // Also stop any pause processes
        if (pauseProcesses != null)
        {
            foreach (var process in pauseProcesses)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            pauseProcesses.Clear();
        }

        // Reset pause states
        foreach (var processInfo in ProcessInfos)
        {
            processInfo.IsPaused = false;
            processInfo.PauseStartTime = null;
        }
    }

    public async Task<bool> PauseStream(string? customMediaPath = null)
    {
        try
        {
            // Get the media path - either custom or default
            var mediaPath = customMediaPath ?? _pauseInterjectService.GetDefaultPauseMedia();

            if (string.IsNullOrEmpty(mediaPath))
            {
                _logger.Log("No pause media configured. Please set a default pause media or provide a custom one.");
                return false;
            }

            if (!_pauseInterjectService.ValidateMediaFile(mediaPath))
            {
                _logger.Log($"Invalid pause media file: {mediaPath}");
                return false;
            }

            _logger.Log($"Pausing all streams with media: {mediaPath}");

            // Stop current live streams
            if (ffmpegProcess != null)
            {
                foreach (var process in ffmpegProcess)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }

            // Start pause streams for each service
            pauseProcesses?.Clear();
            foreach (var processInfo in ProcessInfos)
            {
                if (processInfo.Process != null)
                {
                    // Extract the service details from the process info
                    var serviceName = processInfo.Header ?? "Unknown";

                    // We need to rebuild the output URL from the original process
                    // For now, we'll get it from the selected services
                    // This is a limitation - in production, you'd want to store the output URL in ProcessInfo

                    processInfo.IsPaused = true;
                    processInfo.InterjectMediaPath = mediaPath;
                    processInfo.PauseStartTime = DateTime.UtcNow;

                    _logger.Log($"Marked {serviceName} as paused");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error pausing streams: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PauseStreamToService(string serviceName, ObservableCollection<SelectedService> services, string? customMediaPath = null)
    {
        try
        {
            var mediaPath = customMediaPath ?? _pauseInterjectService.GetDefaultPauseMedia();

            if (string.IsNullOrEmpty(mediaPath))
            {
                _logger.Log("No pause media configured.");
                return false;
            }

            if (!_pauseInterjectService.ValidateMediaFile(mediaPath))
            {
                return false;
            }

            var processInfo = ProcessInfos.FirstOrDefault(p => p.Header == serviceName);
            if (processInfo == null)
            {
                _logger.Log($"No active stream found for service: {serviceName}");
                return false;
            }

            var service = services.FirstOrDefault(s => s.DisplayName == serviceName);
            if (service?.SelectedServer == null)
            {
                _logger.Log($"No server configuration found for service: {serviceName}");
                return false;
            }

            // Build output URL
            string fullUrl;
            if (service.SelectedServer.Url.StartsWith("rtmps://"))
            {
                fullUrl = $"{service.SelectedServer.Url}:443/app/{service.StreamKey}";
            }
            else
            {
                fullUrl = $"{service.SelectedServer.Url}/{service.StreamKey}";
            }

            // Kill the current live stream process
            if (processInfo.Process != null && !processInfo.Process.HasExited)
            {
                processInfo.Process.Kill();
            }

            // Start pause stream
            var pauseProcess = await _pauseInterjectService.StartPauseStream(
                mediaPath,
                fullUrl,
                serviceName,
                service.ServiceGroup?.RecommendedSettings?.MaxVideoBitRate,
                service.ServiceGroup?.RecommendedSettings?.MaxAudioBitRate,
                service.ServiceGroup?.RecommendedSettings?.KeyInt
            );

            if (pauseProcess != null)
            {
                pauseProcesses?.Add(pauseProcess);
                processInfo.Process = pauseProcess;
                processInfo.IsPaused = true;
                processInfo.InterjectMediaPath = mediaPath;
                processInfo.PauseStartTime = DateTime.UtcNow;

                _logger.Log($"Paused stream for {serviceName} with media: {mediaPath}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error pausing stream for {serviceName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResumeStream(ObservableCollection<SelectedService> services)
    {
        try
        {
            _logger.Log("Resuming all paused streams...");

            // Stop pause processes
            if (pauseProcesses != null)
            {
                foreach (var process in pauseProcesses)
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.StandardInput.WriteLine("q");
                            process.StandardInput.Flush();
                            if (!process.WaitForExit(2000))
                            {
                                process.Kill();
                            }
                        }
                        catch
                        {
                            process.Kill();
                        }
                    }
                }
                pauseProcesses.Clear();
            }

            // Restart live streams
            foreach (var processInfo in ProcessInfos.Where(p => p.IsPaused))
            {
                var service = services.FirstOrDefault(s => s.DisplayName == processInfo.Header);
                if (service?.SelectedServer != null)
                {
                    await RestartLiveStream(processInfo, service);
                    processInfo.IsPaused = false;
                    processInfo.InterjectMediaPath = null;
                    processInfo.PauseStartTime = null;
                }
            }

            _logger.Log("All streams resumed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error resuming streams: {ex.Message}");
            return false;
        }
    }

    private async Task RestartLiveStream(StreamProcessInfo processInfo, SelectedService service)
    {
        var path = "Dependencies/ffmpeg";
        var input = RtmpAdress;

        string fullUrl;
        if (service.SelectedServer!.Url.StartsWith("rtmps://"))
        {
            fullUrl = $"{service.SelectedServer.Url}:443/app/{service.StreamKey}";
        }
        else
        {
            fullUrl = $"{service.SelectedServer.Url}/{service.StreamKey}";
        }

        var args = new StringBuilder($"-i \"{input}\" ");
        args.Append("-c:v copy ");

        if (service.ServiceGroup?.RecommendedSettings != null)
        {
            var recommended = service.ServiceGroup.RecommendedSettings;

            if (recommended.MaxVideoBitRate != null)
            {
                args.Append($"-b:v {recommended.MaxVideoBitRate}k ");
            }

            if (recommended.KeyInt != null)
            {
                args.Append($"-g {recommended.KeyInt} ");
            }

            if (recommended.MaxAudioBitRate != null)
            {
                args.Append($"-b:a {recommended.MaxAudioBitRate}k ");
            }
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
            process.Start();

            // Update the process info
            processInfo.Process = process;

            // Add to the live process list
            ffmpegProcess?.Add(process);

            _logger.Log($"Restarted live stream for {processInfo.Header}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to restart live stream for {processInfo.Header}: {ex.Message}");
        }
    }

    private void ApplyGenericMetadataArgs(StringBuilder args, StreamMetadata metadata, SelectedService service)
    {
        if (metadata == null)
        {
            return;
        }

        var capability = ServiceMetadataCapabilities.Resolve(service.ServiceGroup?.ServiceName);
        if (capability.SupportLevel != MetadataSupportLevel.EmbeddedStreamMetadata)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Title))
        {
            args.Append($"-metadata title=\"{EscapeMetadataValue(metadata.Title)}\" ");
        }

        if (metadata.Tags is { Count: > 0 })
        {
            var tagValue = string.Join(",", metadata.Tags.Where(t => !string.IsNullOrWhiteSpace(t)));
            if (!string.IsNullOrWhiteSpace(tagValue))
            {
                args.Append($"-metadata comment=\"{EscapeMetadataValue(tagValue)}\" ");
            }
        }
    }

    private static string EscapeMetadataValue(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
