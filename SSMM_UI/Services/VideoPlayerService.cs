using System;

namespace SSMM_UI.Services;

public class VideoPlayerService : IDisposable
{
    private MyVideoView? _videoView;
    private bool _isInitialized;
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";

    public void RegisterVideoView(MyVideoView videoView)
    {
        if (_isInitialized)
            throw new InvalidOperationException("VideoView kan bara registreras en gång");

        _videoView = videoView;
        _videoView.Play(RtmpAdress);
        _isInitialized = true;
    }

    public void ToggleVisibility(bool isVisible)
    {
        if (_videoView == null) return;
        _videoView.IsVisible = isVisible;
    }

    public void Dispose()
    {
        _videoView?.Stop();
        _videoView = null;
    }
}