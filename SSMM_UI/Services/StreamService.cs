using SSMM_UI.MetaData;
using SSMM_UI.RTMP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class StreamService
{
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

    private RTMPServer Server { get; set; } = new();
    private readonly List<Process>? ffmpegProcess = [];
    private readonly BroadCastService _broadCastService;
    private readonly ILogService _logger;
    public List<StreamProcessInfo> ProcessInfos { get; private set; } = new List<StreamProcessInfo>();
    public StreamService(ILogService logger, BroadCastService broadCastService)
    {
        Server.StartSrv();
        _logger = logger;
        _broadCastService = broadCastService;
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
                        var (newUrl, newKey) = await _broadCastService.CreateYouTubeBroadcastAsync(metadata);

                        // Uppdatera service med nya värden så vi kör rätt stream
                        service.SelectedServer.Url = newUrl;
                        service.StreamKey = newKey;
                    }
                    if (service.DisplayName.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
                    {
                        var (newUrl, newKey) = await _broadCastService.CreateTwitchBroadcastAsync(metadata);

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
                        await BroadCastService.CreateKickBroadcastAsync(metadata);
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
        }
        //ReadOutPut();
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
