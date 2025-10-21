using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SSMM_UI.Interfaces;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchTokenToken : IAuthToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public List<string> Scope { get; set; } = [];

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    public string? Username { get; set; }

    public string? UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAt > DateTime.UtcNow;
    [JsonIgnore]
    public string? ErrorMessage { get; set; }
}
