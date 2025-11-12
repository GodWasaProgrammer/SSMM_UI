using System.Text.Json.Serialization;

namespace SSMM_UI.MetaData;

public class TwitchChannelUpdate
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("game_id")]
    public string? GameId { get; set; }

    [JsonPropertyName("broadcaster_language")]
    public string? BroadcasterLanguage { get; set; }

    // Andra valfria fält som Twitch API stöder
}