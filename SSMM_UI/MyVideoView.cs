using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Shared;
using System;

namespace SSMM_UI;

public class MyVideoView : NativeControlHost
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private IntPtr _hwnd;
    private Media? _media;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hwnd = parent.Handle;

        if (_libVLC == null)
            _libVLC = new LibVLC();

        if (_mediaPlayer == null)
        {
            _mediaPlayer = new MediaPlayer(_libVLC)
            {
                Hwnd = _hwnd
            };
        }

        return parent;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _mediaPlayer?.Stop();
        _media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();

        base.DestroyNativeControlCore(control);
    }

    public void Play(string url)
    {
        if (_mediaPlayer == null || _libVLC == null)
            return;

        _media?.Dispose();
        _media = new Media(_libVLC, url, FromType.FromLocation);

        _mediaPlayer.Play(_media);
    }

    public void Stop()
    {
        if (_mediaPlayer == null)
            return;

        _mediaPlayer.Stop();
        _media?.Dispose();
        _media = null;
    }
}
