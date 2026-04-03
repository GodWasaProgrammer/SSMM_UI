using FFmpeg.AutoGen;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class PollService
{
    public PollService()
    {

    }

    public event Action<bool>? ServerStatusChanged;
    public event Action<bool>? StreamStatusChanged;
    private CancellationTokenSource? _streamPollingCts;
    private CancellationTokenSource? _serverPollingCts;
    private Task? _streamPollingTask;
    private Task? _serverPollingTask;

    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

    public void StartStreamPolling()
    {
        if (_streamPollingTask is { IsCompleted: false })
        {
            return;
        }

        _streamPollingCts = new CancellationTokenSource();
        _streamPollingTask = StartStreamStatusPolling(_streamPollingCts.Token);
    }

    public void StartServerPolling()
    {
        if (_serverPollingTask is { IsCompleted: false })
        {
            return;
        }

        _serverPollingCts = new CancellationTokenSource();
        _serverPollingTask = StartServerStatusPolling(_serverPollingCts.Token);
    }

    public void StopStreamPolling()
    {
        if (_streamPollingCts == null)
        {
            return;
        }

        _streamPollingCts.Cancel();
        _streamPollingCts.Dispose();
        _streamPollingCts = null;
        _streamPollingTask = null;
    }

    public void StopServerPolling()
    {
        if (_serverPollingCts == null)
        {
            return;
        }

        _serverPollingCts.Cancel();
        _serverPollingCts.Dispose();
        _serverPollingCts = null;
        _serverPollingTask = null;
    }

    private async Task StartStreamStatusPolling(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var isAlive = await Task.Run(() => CheckStreamIsAlive(RtmpAdress), cancellationToken);
                StreamStatusChanged?.Invoke(isAlive);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                StreamStatusChanged?.Invoke(false);
            }

            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
    private static async Task<bool> IsRtmpApiResponding()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                    request?.RequestUri?.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true ||
                    errors == SslPolicyErrors.None
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(5);
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
    private async Task StartServerStatusPolling(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool isResponding = await IsRtmpApiResponding(); // Använd await istället för .Result

            ServerStatusChanged?.Invoke(isResponding);

            try
            {
                await Task.Delay(5000, cancellationToken); // 5 sekunders delay
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
