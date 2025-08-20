using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.Oauth.Twitch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class CentralAuthService
{
    private GoogleOAuthService GoogleAuthService { get; set; }
    public TwitchDCAuthService TwitchService;
    private readonly KickOAuthService? _kickOauthService;
    private ILogService _logger;

    public CentralAuthService(ILogService logger)
    {
        _kickOauthService = new(logger);
        GoogleAuthService = new(logger);
        var scopes = new[]
        {
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        };
        _logger = logger;
        TwitchService = new TwitchDCAuthService(_logger);
    }

    public async Task<string> LoginWithTwitch()
    {
        if (TwitchService == null)
        {
            throw new Exception("TwitchService was null!");
        }
        var IsTokenValid = TwitchService.TryLoadValidOrRefreshTokenAsync();

        string LoginResult = "";
        if (IsTokenValid.Result != null)
        {
            if (IsTokenValid.Result != null)
            {
                LoginResult = ($"✅ Inloggad som: {IsTokenValid.Result.UserName}");
            }
        }
        else
        {
            var device = await TwitchService.StartDeviceCodeFlowAsync();

            Process.Start(new ProcessStartInfo
            {
                FileName = device.VerificationUri,
                UseShellExecute = true
            });
            // Visa UI eller vänta medan användaren godkänner
            var token = await TwitchService.PollForTokenAsync(device.DeviceCode, device.Interval);

            if (token != null)
            {
                LoginResult = ($"✅ Inloggad som: {token.UserName}");
            }
            else
            {
                _logger.Log("Timeout - användaren loggade inte in.");
            }
        }
        return LoginResult;
    }
    public async Task<(string, YouTubeService? _uTube)> LoginWithYoutube()
    {
        YouTubeService ytService = null;
        string username = "Failed to log in";
        try
        {

            if (GoogleAuthService == null)
            {
                throw new Exception("Google Auth Service was null");
            }
            else
            {
                var res = await GoogleAuthService.LoginWithYoutube();
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
        catch
        (Exception ex)
        {
            _logger.Log(ex.Message);
        }
        return ("Unable to login to youtube", ytService);
    }
    public async Task<string> LoginWithKick()
    {
        // Ange vilka scopes du behöver
        var requestedScopes = new[]
        {
        KickOAuthService.Scopes.ChannelWrite,
        KickOAuthService.Scopes.ChannelRead,
        KickOAuthService.Scopes.UserRead
    };

        if (_kickOauthService == null)
            return "❌ KickAuthService är inte initialiserad.";

        try
        {
            var result = await _kickOauthService.AuthenticateOrRefreshAsync(requestedScopes);

            if (result != null)
            {
                return $"✅ Inloggad som {result.Username}";
            }
            else
            {
                return "❌ Inloggning misslyckades – token saknas eller ogiltig.";
            }
        }
        catch (Exception ex)
        {
            // Logga för felsökning
            _logger.Log($"❌ Kick-login error: {ex.Message}");
            return $"❌ Fel vid inloggning: {ex.Message}";
        }
    }

    public async Task<(List<AuthResult>, YouTubeService?)> TryAutoLoginAllAsync()
    {
        var results = new List<AuthResult>();
        GoogleOauthResult GoogleAuthResult = new();
        YouTubeService ytService = null;
        try
        {

            // Twitch
            var twitchToken = await TwitchService.TryLoadValidOrRefreshTokenAsync();

            if (twitchToken.ErrorMessage == null)
            {
                results.Add(twitchToken is not null
                    ? new AuthResult(AuthProvider.Twitch, true, twitchToken.UserName, null)
                    : new AuthResult(AuthProvider.Twitch, false, null, "Token was missing or is invalid"));
            }
            else
            {
                results.Add(new AuthResult(AuthProvider.Twitch, false, null, twitchToken.ErrorMessage));
            }

            // Google/YouTube
            GoogleAuthResult = await GoogleAuthService.LoginAutoIfTokenized();
            results.Add(GoogleAuthResult is not null
                ? new AuthResult(AuthProvider.YouTube, true, GoogleAuthResult.Username, null)
                : new AuthResult(AuthProvider.YouTube, false, null, "Token was missing or is invalid"));

            // Kick
            var kickToken = await _kickOauthService.IfTokenIsValidLoginAuto();
            results.Add(kickToken is not null
                ? new AuthResult(AuthProvider.Kick, true, kickToken.Username, null)
                : new AuthResult(AuthProvider.Kick, false, null, "Token was missing or is invalid"));

        }
        catch (Exception ex)
        {
            results.Add(new AuthResult(AuthProvider.YouTube, false, null, ex.Message));
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
        return (results, ytService);
    }
}

public enum AuthProvider
{
    Twitch,
    YouTube,
    Kick
}

public record AuthResult(AuthProvider Provider, bool Success, string? Username, string? ErrorMessage);