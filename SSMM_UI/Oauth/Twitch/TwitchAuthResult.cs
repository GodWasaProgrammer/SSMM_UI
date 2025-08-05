using System;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchAuthResult
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Scope { get; set; } = "";
    public string TokenType { get; set; } = "";
    public string Username { get; set; } = "";

    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && ExpiresAt > DateTime.UtcNow.AddMinutes(5);
}