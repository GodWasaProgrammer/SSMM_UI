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
}