using System;
using System.Collections.Generic;

namespace SSMM_UI.RTMP;

public enum MetadataSupportLevel
{
    FullPlatformIntegration,
    EmbeddedStreamMetadata,
    PartialPlatformIntegration
}

public sealed class ServiceMetadataCapability
{
    public required string ServiceName { get; init; }
    public required MetadataSupportLevel SupportLevel { get; init; }
    public required string Reason { get; init; }
}

public static class ServiceMetadataCapabilities
{
    private static readonly HashSet<string> FullPlatformServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "YouTube - HLS",
        "YouTube - RTMPS",
        "Twitch",
        "Kick"
    };

    private static readonly HashSet<string> PartialPlatformServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trovo",
        "Facebook Live"
    };

    // This list mirrors the current services catalog from SSMM_UI/services.json.
    private static readonly HashSet<string> CatalogServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "AfreecaTV",
        "Amazon IVS",
        "AngelThump",
        "Aparat",
        "api.video",
        "Bilibili Live - RTMP | 哔哩哔哩直播 - RTMP",
        "Bitmovin",
        "Bongacams",
        "Boomstream",
        "BoxCast",
        "Breakers.TV",
        "CAM4",
        "CamSoda",
        "Castr.io",
        "Chaturbate",
        "CHZZK",
        "Dacast",
        "Disciple Media",
        "DLive",
        "Dolby Millicast",
        "Enchant.events",
        "ePlay",
        "Eventials",
        "EventLive.pro",
        "Facebook Live",
        "GoodGame.ru",
        "IRLToolkit",
        "Jio Games",
        "Joystick.TV",
        "KakaoTV",
        "Kick",
        "Konduit.live",
        "Kuaishou Live",
        "Lahzenegar - StreamG | لحظه‌نگار - استریمجی",
        "Lightcast.com",
        "Live Streamer Cafe",
        "Livepeer Studio",
        "Livepush",
        "Livestream",
        "LOCO",
        "Loola.tv",
        "Lovecast",
        "Luzento.com - RTMP",
        "MasterStream.iR | مستراستریم | ری استریم و استریم همزمان",
        "Meridix Live Sports Platform",
        "Mixcloud",
        "Mux",
        "MyFreeCams",
        "MyLive",
        "nanoStream Cloud / bintu",
        "NFHS Network",
        "niconico (ニコニコ生放送)",
        "Nimo TV",
        "OnlyFans.com",
        "OPENREC.tv - Premium member (プレミアム会員)",
        "PandaTV | 팬더티비",
        "PhoneLiveStreaming",
        "Picarto",
        "Piczel.tv",
        "Playeur",
        "PolyStreamer.com",
        "Restream.io",
        "SermonAudio Cloud",
        "SharePlay.tv",
        "sheeta",
        "SHOWROOM",
        "STAGE TEN",
        "Steam",
        "Streamway",
        "Stripchat",
        "Switchboard Live",
        "Sympla",
        "Trovo",
        "Twitch",
        "Twitter",
        "Uscreen",
        "Vaughn Live / iNSTAGIB",
        "Vault - by CommanderRoot",
        "Viloud",
        "Vimeo",
        "Vindral",
        "Volume.com",
        "VRCDN - Live",
        "Web.TV",
        "Whowatch (ふわっち)",
        "WpStream",
        "XLoveCam.com",
        "YouTube - HLS",
        "YouTube - RTMPS"
    };

    public static ServiceMetadataCapability Resolve(string? serviceName)
    {
        var normalized = serviceName?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return new ServiceMetadataCapability
            {
                ServiceName = "Unknown",
                SupportLevel = MetadataSupportLevel.EmbeddedStreamMetadata,
                Reason = "Service name was missing; using embedded stream metadata fallback."
            };
        }

        if (FullPlatformServices.Contains(normalized))
        {
            return new ServiceMetadataCapability
            {
                ServiceName = normalized,
                SupportLevel = MetadataSupportLevel.FullPlatformIntegration,
                Reason = "Uses dedicated platform metadata integration."
            };
        }

        if (PartialPlatformServices.Contains(normalized))
        {
            return new ServiceMetadataCapability
            {
                ServiceName = normalized,
                SupportLevel = MetadataSupportLevel.PartialPlatformIntegration,
                Reason = "Platform endpoint exists but full metadata path is not finalized."
            };
        }

        if (CatalogServices.Contains(normalized))
        {
            return new ServiceMetadataCapability
            {
                ServiceName = normalized,
                SupportLevel = MetadataSupportLevel.EmbeddedStreamMetadata,
                Reason = "Catalog service currently uses generic ffmpeg metadata embedding."
            };
        }

        return new ServiceMetadataCapability
        {
            ServiceName = normalized,
            SupportLevel = MetadataSupportLevel.EmbeddedStreamMetadata,
            Reason = "Service is outside catalog; applying generic ffmpeg metadata embedding."
        };
    }
}
