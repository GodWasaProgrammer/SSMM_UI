using Google.Apis.YouTube.v3;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.Oauth.Twitch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class CentralAuthService
{
    public YouTubeService? YTService { get; set; }
    public GoogleOAuthService? GoogleAuthService { get; set; }
    public TwitchDCAuthService TwitchService;
    private readonly KickOAuthService? _kickOauthService;

    public CentralAuthService()
    {
        _kickOauthService = new KickOAuthService();
        var scopes = new[]
        {
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        };
        TwitchService = new TwitchDCAuthService(new HttpClient(), scopes);
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
                LogService.Log("Timeout - användaren loggade inte in.");
            }
        }
        return LoginResult;
    }
    public async Task<string> LoginWithYoutube()
    {
        GoogleAuthService = new();
        return await GoogleAuthService.LoginWithYoutube();
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
            LogService.Log($"❌ Kick-login error: {ex.Message}");
            return $"❌ Fel vid inloggning: {ex.Message}";
        }
    }

    public async Task<List<AuthResult>> TryAutoLoginAllAsync()
    {
        var results = new List<AuthResult>();

        // Twitch
        var twitchToken = await TwitchService.TryLoadValidOrRefreshTokenAsync();
        results.Add(twitchToken is not null
            ? new AuthResult(AuthProvider.Twitch, true, twitchToken.UserName, null)
            : new AuthResult(AuthProvider.Twitch, false, null, "Token saknas eller ogiltig"));

        // Google/YouTube
        try
        {
            // TODO: make sure that we instantiate YTService also or shit will break down the pipe
            GoogleAuthService ??= new();
            var user = await GoogleAuthService.LoginAutoIfTokenized();
            results.Add(new AuthResult(AuthProvider.YouTube, true, user, null));
        }
        catch (Exception ex)
        {
            results.Add(new AuthResult(AuthProvider.YouTube, false, null, ex.Message));
        }

        // Kick
        var kickToken = await KickOAuthService.IfTokenIsValidLoginAuto();
        results.Add(kickToken is not null
            ? new AuthResult(AuthProvider.Kick, true, kickToken.Username, null)
            : new AuthResult(AuthProvider.Kick, false, null, "Token saknas eller ogiltig"));

        return results;
    }
}

public enum AuthProvider
{
    Twitch,
    YouTube,
    Kick
}

public record AuthResult(AuthProvider Provider, bool Success, string? Username, string? ErrorMessage);