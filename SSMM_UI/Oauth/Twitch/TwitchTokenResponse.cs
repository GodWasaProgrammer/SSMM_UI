using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchTokenTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public List<string> Scope { get; set; } = new();

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    public string? UserName { get; set; }

    public string? UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAt > DateTime.UtcNow;
    [JsonIgnore]
    public string? ErrorMessage { get; set; }
}
