using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.Enums;
using SSMM_UI.Oauth.Facebook;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.Oauth.Twitch;
using SSMM_UI.Oauth.X;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class CentralAuthService
{
    private GoogleAuthService GoogleAuthService { get; set; }
    public TwitchDCAuthService TwitchService;
    private readonly KickAuthService? _kickOauthService;
    private XAuthService XOAuth { get; set; }
    private readonly FacebookAuthService fbAuth;
    private readonly ILogService _logger;
    private readonly StateService _stateService;

    public CentralAuthService(ILogService logger, StateService stateService)
    {
        _stateService = stateService;
        _logger = logger;
        _kickOauthService = new(_logger, _stateService);
        GoogleAuthService = new(_logger, _stateService);
        XOAuth = new(_logger, _stateService);
        TwitchService = new(_logger, _stateService);
        fbAuth = new(_logger, _stateService);
    }

    public async Task<string> FacebookLogin()
    {
        var res = await fbAuth.LoginAsync();
        if (res != null)
        {
            return $"✅ Logged in as: {res.Username}";
        }
        else
        {
            return "❌ Login failed.";
        }
    }

    public async Task<string> LoginWithX()
    {
        if (XOAuth == null)
        {
            throw new Exception("XOAuth was null!");
        }
        var loginResult = await XOAuth.LoginAsync();

        if (loginResult != null)
        {
            return $"✅ Logged in as: {loginResult.Username}";
        }
        else
        {
            return "❌ Login failed.";
        }
    }

    public async Task<string?> LoginWithTwitch()
    {
        if (TwitchService == null)
        {
            throw new Exception("TwitchService was null!");
        }

        var token = await TwitchService.LoginAsync();

        if (token != null)
        {
            var LoginResult = ($"✅ Logged in as: {token.Username}");
            return LoginResult;
        }
        else
        {
            _logger.Log("Timeout - user did not log in");
        }

        return null;
    }
    public async Task<(string, YouTubeService? _uTube)> LoginWithYoutube()
    {
        YouTubeService? ytService = null;
        string username;
        try
        {
            if (GoogleAuthService == null)
            {
                throw new Exception("Google Auth Service was null");
            }
            else
            {
                var res = await GoogleAuthService.LoginAsync();
                if (res != null)
                {
                    username = res.Username;
                    if (res.AccessToken != null)
                    {
                        var credential = GoogleCredential.FromAccessToken(res.AccessToken);
                        var YTService = new YouTubeService(new BaseClientService.Initializer
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = "Streamer & Social Media Manager"
                        });
                        ytService = YTService;

                    }
                    return (username, ytService);
                }
            }
        }
        catch
        (Exception ex)
        {
            _logger.Log(ex.Message);
        }
        return ("Unable to login to youtube", ytService);
    }
    public async Task<string> LoginWithKick()
    {
        if (_kickOauthService == null)
            return "❌ KickAuthService is not initialized.";

        try
        {
            var result = await _kickOauthService.LoginAsync();

            if (result != null)
            {
                return $"✅ logged in as: {result.Username}";
            }
            else
            {
                return "❌ login failed – token missing or is invalid.";
            }
        }
        catch (Exception ex)
        {
            // Logga för felsökning
            _logger.Log($"❌ Kick-login error: {ex.Message}");
            return $"❌ error logging in: {ex.Message}";
        }
    }

    public async Task<List<AuthResult>> TryAutoLoginSocialMediaAsync()
    {
        var results = new List<AuthResult>();

        var xToken = await XOAuth.TryUseExistingTokenAsync();
        results.Add(xToken is not null
            ? new AuthResult(AuthProvider.X, true, xToken.Username, null)
            : new AuthResult(AuthProvider.X, false, null, "Token was missing or is invalid"));
        var fbToken = await fbAuth.TryUseExistingTokenAsync();
        results.Add(fbToken is not null
            ? new AuthResult(AuthProvider.Facebook, true, fbToken.Username, null)
            : new AuthResult(AuthProvider.Facebook, false, null, "Token was missing or is invalid"));

        return results;
    }

    public async Task<(List<AuthResult?>, YouTubeService?)> TryAutoLoginStreamServicesAsync()
    {
        var results = new List<AuthResult>();
        GoogleToken? GoogleAuthResult = new();
        YouTubeService? ytService = null;
        try
        {

            // Twitch
            var twitchToken = await TwitchService.TryUseExistingTokenAsync();

            if (twitchToken != null)
            {

                if (twitchToken.ErrorMessage == null)
                {
                    results.Add(twitchToken is not null
                        ? new AuthResult(AuthProvider.Twitch, true, twitchToken.Username, null)
                        : new AuthResult(AuthProvider.Twitch, false, null, "Token was missing or is invalid"));
                }
                else
                {
                    results.Add(new AuthResult(AuthProvider.Twitch, false, null, twitchToken.ErrorMessage));
                }
            }
            else
            {
                results.Add(new AuthResult(AuthProvider.Twitch, false, null, "Token was missing or is invalid"));
            }

            // Google/YouTube
            GoogleAuthResult = await GoogleAuthService.TryUseExistingTokenAsync();
            results.Add(GoogleAuthResult is not null
                ? new AuthResult(AuthProvider.YouTube, true, GoogleAuthResult.Username, null)
                : new AuthResult(AuthProvider.YouTube, false, null, "Token was missing or is invalid"));

            // Kick
            if (_kickOauthService != null)
            {
                var kickToken = await _kickOauthService.TryUseExistingTokenAsync();
                results.Add(kickToken is not null
                    ? new AuthResult(AuthProvider.Kick, true, kickToken.Username, null)
                    : new AuthResult(AuthProvider.Kick, false, null, "Token was missing or is invalid"));
            }

        }
        catch (Exception ex)
        {
            _logger.Log($"AutoLoginStreamServicesAsync failed: {ex.Message}");
        }
        if (GoogleAuthResult != null)
        {

            if (GoogleAuthResult.AccessToken != null)
            {
                var credential = GoogleCredential.FromAccessToken(GoogleAuthResult.AccessToken);
                var YTService = new YouTubeService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Streamer & Social Media Manager"
                });
                ytService = YTService;

            }
        }
        return (results, ytService)!;
    }
}
public record AuthResult(AuthProvider Provider, bool Success, string? Username, string? ErrorMessage);