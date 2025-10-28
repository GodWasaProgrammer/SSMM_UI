using SSMM_UI.Interfaces;
using System;
using System.Text.Json.Serialization;

namespace SSMM_UI.Oauth.X;

public class XToken : IAuthToken
{
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "bearer";

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public DateTimeOffset ExpiresAtOffset => CreatedAt.AddSeconds(ExpiresIn);

    [JsonIgnore]
    DateTime IAuthToken.ExpiresAt => ExpiresAtOffset.UtcDateTime;

    [JsonIgnore]
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtOffset - TimeSpan.FromMinutes(2);

    // Kan sättas efter /2/users/me-anrop
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && TokenType.Equals("bearer", StringComparison.OrdinalIgnoreCase)
        && !IsExpired;

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        var preview = AccessToken?.Length > 8 ? AccessToken[..8] + "..." : AccessToken;
        return $"XToken(Type={TokenType}, ExpiresAt={ExpiresAtOffset:u}, Valid={IsValid}, User={Username ?? "?"}, Token={preview})";
    }
}
