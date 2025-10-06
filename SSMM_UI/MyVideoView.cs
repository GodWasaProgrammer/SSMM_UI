using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Shared;
using System;

namespace SSMM_UI;

public class MyVideoView : NativeControlHost
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private IntPtr _hwnd;
    private Media? _media;

    public MyVideoView()
    {
        if (App.SharedLibVLC != null)
        {
            _libVLC = App.SharedLibVLC;
            _mediaPlayer = new MediaPlayer(_libVLC);
        }
        else
        {
            throw new Exception("we done fucked up");
        }

    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // Här får vi native hwnd
        _hwnd = parent.Handle;

        // Sätt hwnd för mediaPlayer så det kan visa video i rätt kontroll
        _mediaPlayer.Hwnd = _hwnd;

        return parent;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _mediaPlayer.Stop();
        _media?.Dispose();
        _mediaPlayer.Dispose();
        // Disposar ej LibVLC eftersom den är delad

        base.DestroyNativeControlCore(control);
    }

    public void Play(string url)
    {
        if (_mediaPlayer == null)
            return;

        _media?.Dispose();
        _media = new Media(_libVLC, url, FromType.FromLocation);

        _mediaPlayer.Play(_media);
        _mediaPlayer.Volume = 0;
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
