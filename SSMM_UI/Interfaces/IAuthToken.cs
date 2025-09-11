using System;

namespace SSMM_UI.Interfaces;

public interface IAuthToken
{
    string AccessToken { get; }
    string RefreshToken { get; }
    string TokenType { get; }
    DateTime ExpiresAt { get; }
    string? Username { get; }
    bool IsValid { get; }
    string? ErrorMessage { get; }
}
