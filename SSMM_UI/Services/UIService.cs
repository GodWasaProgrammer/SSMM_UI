using System;

namespace SSMM_UI.Services;

public static class UIService
{
    public static event Action<bool>? StartStreamButtonChanged;
    public static void StartBroadCastStream(bool broadCast)
    {
        var StatusOfBroadcast = broadCast;
        StartStreamButtonChanged?.Invoke(StatusOfBroadcast);
    }

    public static event Action<bool>? StopStreamButtonChanged;
    public static void ToggleStopStreamButton(bool StopStreamButton)
    {
        var StatusOfStopStreamButton = StopStreamButton;
        StopStreamButtonChanged?.Invoke(StatusOfStopStreamButton);
    }

    public static event Action<string>? ServerStatusChanged;
    public static void UpdateServerStatus(bool IsAlive)
    {
        var text = IsAlive
            ? "RTMP-server: ✅ Running"
            : "RTMP-server: ❌ Not Running";
        ServerStatusChanged?.Invoke(text);
    }

    public static event Action<string>? StreamStatusChanged;
    public static void UpdateStreamStatus(bool isAlive)
    {
        var text = isAlive
            ? "Stream status: ✅ Live"
            : "Stream status: ❌ Not Receiving";

        StreamStatusChanged?.Invoke(text);
    }
}
