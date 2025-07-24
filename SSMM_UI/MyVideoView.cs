using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Shared;

namespace SSMM_UI;
public class MyVideoView : NativeControlHost
{
    private LibVLC _libVLC;
    private MediaPlayer _mediaPlayer;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // Här hämtar vi Avalonia-fönstrets native HWND
        var hwnd = parent.Handle;

        // Initiera LibVLC och MediaPlayer
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);

        // Skapa Media och starta uppspelning
        var media = new Media(_libVLC, "rtmp://localhost/live/stream", FromType.FromLocation);
        _mediaPlayer.Play(media);

        // Koppla VLC:s video output till Avalonia native hwnd
        _mediaPlayer.Hwnd = hwnd;

        return parent;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.DestroyNativeControlCore(control);
    }
}