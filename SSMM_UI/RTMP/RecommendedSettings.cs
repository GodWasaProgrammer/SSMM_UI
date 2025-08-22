using System.Text.Json.Serialization;

namespace SSMM_UI.RTMP;

public class RecommendedSettings
{
    [JsonPropertyName("keyint")]
    public int? KeyInt { get; set; }

    [JsonPropertyName("max video bitrate")]
    public int? MaxVideoBitRate { get; set; }

    [JsonPropertyName("max audio bitrate")]
    public int? MaxAudioBitRate { get; set; }

    [JsonPropertyName("supported video codecs")]
    public string[]? SupportedVideoCodes { get; set; }
}
