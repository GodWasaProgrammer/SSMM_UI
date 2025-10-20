using SSMM_UI.Interfaces;
using System;
using System.Text.Json.Serialization;

namespace SSMM_UI.Oauth.Facebook;

public class FacebookToken : IAuthToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsLongLived { get; set; }

    [JsonIgnore]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public DateTimeOffset ExpiresAtOffset => CreatedAt.AddSeconds(ExpiresIn);

    [JsonIgnore]
    DateTime IAuthToken.ExpiresAt => ExpiresAtOffset.UtcDateTime;

    [JsonIgnore]
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtOffset - TimeSpan.FromMinutes(2);

    [JsonIgnore]
    public string? Username { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        var type = IsLongLived ? "long" : "short";
        var preview = AccessToken?.Length > 8 ? AccessToken[..8] + "..." : AccessToken;
        return $"FacebookToken(User={Username ?? "?"}, Type={type}, Expires={ExpiresAtOffset:u}, Valid={IsValid}, Token={preview})";
    }
}
