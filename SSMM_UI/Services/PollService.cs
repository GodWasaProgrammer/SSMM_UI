using FFmpeg.AutoGen;
using System;
using System.Diagnostics;
using System.Net.Http;
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
    private readonly CancellationTokenSource _cts = new();

    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

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
}
