using Avalonia.Controls;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.Dialogs;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.Oauth.Twitch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class CentralAuthService
{
    public YouTubeService? YTService { get; set; }
    public GoogleOAuthService? GoogleAuthService { get; set; }
    public TwitchDCAuthService TwitchService;
    private readonly KickOAuthService? _kickOauthService;
    private readonly string _TwitchDCFClientId;

    public CentralAuthService()
    {
        _kickOauthService = new KickOAuthService();
        var TwitchDcfClientId = Environment.GetEnvironmentVariable("TwitchDCFClient");
        if (TwitchDcfClientId is not null)
        {
            _TwitchDCFClientId = TwitchDcfClientId;
        }
        else
        {
            throw new Exception("ClientID for Twitch was missing");
        }
        var scopes = new[]
        {
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        };
        TwitchService = new TwitchDCAuthService(new HttpClient(), _TwitchDCFClientId, scopes);
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
                Console.WriteLine("Timeout - användaren loggade inte in.");
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
            Console.WriteLine($"❌ Kick-login error: {ex.Message}");
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
            //var clientSecrets = new ClientSecrets
            //{
            //    ClientId = Environment.GetEnvironmentVariable("SSMM_ClientID"),
            //    ClientSecret = Environment.GetEnvironmentVariable("SSMM_ClientSecret")
            //};

            //var scopes = new[] {
            //    Oauth2Service.Scope.UserinfoProfile,
            //    Oauth2Service.Scope.UserinfoEmail,
            //    YouTubeService.Scope.Youtube,
            //    YouTubeService.Scope.YoutubeForceSsl
            //};

            //var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            //    clientSecrets, scopes, "user", CancellationToken.None);

            //await credential.RefreshTokenAsync(CancellationToken.None);

            //YTService = new YouTubeService(new BaseClientService.Initializer()
            //{
            //    HttpClientInitializer = credential,
            //    ApplicationName = "SSMM"
            //});

            //var oauth2 = new Oauth2Service(new BaseClientService.Initializer
            //{
            //    HttpClientInitializer = credential,
            //    ApplicationName = "SSMM_UI"
            //});

            //var user = await oauth2.Userinfo.Get().ExecuteAsync();


            // TODO: make sure that we instantiate YTService also or shit will break down the pipe
            if(GoogleAuthService == null)
            {
                GoogleAuthService = new();
            }
            var user = await GoogleAuthService.LoginWithYoutube();
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