using Google.Apis.YouTube.v3;
using SSMM_UI.MetaData;
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
    private GoogleOAuthService GoogleAuthService { get; set; }
    public TwitchDCAuthService TwitchService;
    private readonly KickOAuthService? _kickOauthService;

    public CentralAuthService()
    {
        _kickOauthService = new();
        GoogleAuthService = new();
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
    public async Task<string> LoginWithYoutube(MetaDataService MDService)
    {
        try
        {

            if (GoogleAuthService == null)
            {
                throw new Exception("Google Auth Service was null");
            }
            else
            {
                return await GoogleAuthService.LoginWithYoutube(MDService);
            }
        }
        catch
        (Exception ex)
        {
            LogService.Log(ex.Message);
        }
        return "Unable to login to youtube";
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

    public async Task<List<AuthResult>> TryAutoLoginAllAsync(MetaDataService MDService)
    {
        var results = new List<AuthResult>();
        try
        {

            // Twitch
            var twitchToken = await TwitchService.TryLoadValidOrRefreshTokenAsync();
            results.Add(twitchToken is not null
                ? new AuthResult(AuthProvider.Twitch, true, twitchToken.UserName, null)
                : new AuthResult(AuthProvider.Twitch, false, null, "Token saknas eller ogiltig"));

            // TODO: make sure that we instantiate YTService also or shit will break down the pipe
            // Google/YouTube
            var user = await GoogleAuthService.LoginAutoIfTokenized(MDService);
            results.Add(new AuthResult(AuthProvider.YouTube, true, user, null));

            // Kick
            var kickToken = await KickOAuthService.IfTokenIsValidLoginAuto();
            results.Add(kickToken is not null
                ? new AuthResult(AuthProvider.Kick, true, kickToken.Username, null)
                : new AuthResult(AuthProvider.Kick, false, null, "Token saknas eller ogiltig"));

        }
        catch (Exception ex)
        {
            results.Add(new AuthResult(AuthProvider.YouTube, false, null, ex.Message));
        }
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