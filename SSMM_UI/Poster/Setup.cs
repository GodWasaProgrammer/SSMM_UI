using System.Collections.Generic;

namespace SSMM_UI.Poster;

internal static class Setup
{
    public static readonly List<Platforms> MonitorLive = [];

    public static void ListSetup()
    {
        Platform();
    }
    private static void Platform()
    {
        MonitorLive.Add(Platforms.Twitch);
        MonitorLive.Add(Platforms.Youtube);
    }
}