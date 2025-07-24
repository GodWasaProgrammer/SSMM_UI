using FFmpeg.AutoGen;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI;

public sealed class RtmpReceiver : IDisposable
{
    private unsafe struct ReceiverState
    {
        public AVFormatContext* pFormatContext;
        public AVPacket* packet;
        public bool isReceiving;
    }

    private readonly int _port;
    private unsafe ReceiverState* _state;
    private Task? _receiverTask;
    private CancellationTokenSource? _cts;

    public event Action<string>? OnLogMessage;
    public event Action<byte[], int>? OnVideoDataReceived;

    public RtmpReceiver(int port = 1935)
    {
        _port = port;
        unsafe
        {
            _state = (ReceiverState*)Marshal.AllocHGlobal(sizeof(ReceiverState));
            _state->isReceiving = false;
            _state->pFormatContext = null;
            _state->packet = null;
        }
        ffmpeg.avformat_network_init();
    }

    public unsafe void Start()
    {
        if (_state->isReceiving) return;

        _cts = new CancellationTokenSource();
        _state->isReceiving = true;
        _receiverTask = Task.Run(() => SafeReceiveLoop(_cts.Token));
    }

    private void SafeReceiveLoop(CancellationToken cancellationToken)
    {
        unsafe
        {
            try
            {
                var url = $"rtmp://0.0.0.0:{_port}/live/stream";
                _state->pFormatContext = ffmpeg.avformat_alloc_context();

                // Konfigurera options
                AVDictionary* options = null;
                ffmpeg.av_dict_set(&options, "listen", "1", 0);
                ffmpeg.av_dict_set(&options, "timeout", "5000000", 0);

                int ret = ffmpeg.avformat_open_input(&_state->pFormatContext, url, null, &options);
                if (ret != 0)
                {
                    LogError($"Failed to open input (error: {ret})");
                    return;
                }

                if (ffmpeg.avformat_find_stream_info(_state->pFormatContext, null) < 0)
                {
                    LogError("Failed to find stream info");
                    return;
                }

                LogMessage("Waiting for OBS connection...");

                int videoStreamIndex = -1;
                for (int i = 0; i < _state->pFormatContext->nb_streams; i++)
                {
                    if (_state->pFormatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        videoStreamIndex = i;
                        break;
                    }
                }

                if (videoStreamIndex == -1)
                {
                    LogError("No video stream found");
                    return;
                }

                _state->packet = ffmpeg.av_packet_alloc();

                while (_state->isReceiving && !cancellationToken.IsCancellationRequested)
                {
                    ret = ffmpeg.av_read_frame(_state->pFormatContext, _state->packet);
                    if (ret < 0)
                    {
                        if (ret == ffmpeg.AVERROR_EOF)
                            LogMessage("End of stream");
                        else
                            LogError($"Read frame error: {ret}");

                        Thread.Sleep(100);
                        continue;
                    }

                    if (_state->packet->stream_index == videoStreamIndex)
                    {
                        byte[] buffer = new byte[_state->packet->size];
                        fixed (byte* ptr = buffer)
                        {
                            Buffer.MemoryCopy(_state->packet->data, ptr, _state->packet->size, _state->packet->size);
                            OnVideoDataReceived?.Invoke(buffer, buffer.Length);
                        }
                    }

                    ffmpeg.av_packet_unref(_state->packet);
                }
            }
            catch (Exception ex)
            {
                LogError($"Receiver error: {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        unsafe
        {
            _state->isReceiving = false;
        }

        _cts?.Cancel();

        if (_receiverTask != null)
        {
            await _receiverTask;
        }
    }

    public void Dispose()
    {
        unsafe
        {
            if (_state != null)
            {
                _state->isReceiving = false;

                if (_state->packet != null)
                {
                    ffmpeg.av_packet_free(&_state->packet);
                }

                if (_state->pFormatContext != null)
                {
                    ffmpeg.avformat_close_input(&_state->pFormatContext);
                }

                Marshal.FreeHGlobal((IntPtr)_state);
                _state = null;
            }
        }

        _cts?.Dispose();
        ffmpeg.avformat_network_deinit();
        GC.SuppressFinalize(this);
    }

    private void LogMessage(string message)
    {
        OnLogMessage?.Invoke($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
    }

    private void LogError(string error)
    {
        OnLogMessage?.Invoke($"[ERROR] {DateTime.Now:HH:mm:ss} - {error}");
    }
}
