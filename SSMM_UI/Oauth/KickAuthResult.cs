using System;

namespace SSMM_UI.Oauth;

public class KickAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && ExpiresAt > DateTime.UtcNow.AddMinutes(5);
}
