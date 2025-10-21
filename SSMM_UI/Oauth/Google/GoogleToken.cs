﻿using SSMM_UI.Interfaces;
using System;

namespace SSMM_UI.Oauth.Google;

public class GoogleToken : IAuthToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && ExpiresAt > DateTime.UtcNow.AddMinutes(5);

    public string? ErrorMessage { get; }
}
