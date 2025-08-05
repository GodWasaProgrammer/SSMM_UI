using System.Text.Json.Serialization;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchOAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}
