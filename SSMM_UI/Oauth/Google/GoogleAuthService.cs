using Google.Apis.Oauth2.v2;
using Google.Apis.YouTube.v3;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;

namespace SSMM_UI.Oauth.Google;

public class GoogleAuthService : IOAuthService<GoogleToken>
{
    private const string RedirectUri = "http://localhost:12347/";
    private const string ClientID = "376695458347-d9ieprrigebp9dptdm9asbl33vgg137o.apps.googleusercontent.com";
    private const string OAuthBaseUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserinfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string _userInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";
    private GoogleToken? _oauthResult;
    private static readonly string[] Scopes =
    [
    Oauth2Service.Scope.UserinfoProfile,
    Oauth2Service.Scope.UserinfoEmail,
    YouTubeService.Scope.Youtube,
    YouTubeService.Scope.YoutubeForceSsl
    ];
    private string? _currentCodeVerifier;
    private string? _currentState;

    private readonly ILogService _logger;
    private readonly StateService _stateService;
    public GoogleAuthService(ILogService logger, StateService stateservice)
    {
        _logger = logger;
        _stateService = stateservice;
    }

    public async Task<GoogleToken?> TryUseExistingTokenAsync()
    {
        _oauthResult = _stateService.DeserializeToken<GoogleToken>(OAuthServices.Youtube);
        if (_oauthResult != null)
        {
            var res = await RefreshTokenAsync(_oauthResult.AccessToken);
            if (res is null)
            {
                return null;
            }
            _oauthResult = res;
            var username = await GetUsernameAsync(_oauthResult.AccessToken);
            if (username != null)
            {
                _oauthResult.Username = username;
                return _oauthResult;
            }
        }
        return null;
    }

    public async Task<GoogleToken?> LoginAsync()
    {
        var codeVerifier = PKCEHelper.GenerateCodeVerifier();
        var codeChallenge = PKCEHelper.GenerateCodeChallenge(codeVerifier);
        _currentCodeVerifier = codeVerifier;
        _currentState = PKCEHelper.RandomString(32);

        var AuthUrl = BuildAuthorizationUrl(Scopes, ClientID, RedirectUri, codeChallenge, _currentState);

        OpenBrowser(AuthUrl);

        // listen for callback
        try
        {
            string authCode = await ListenForAuthCodeAsync();
            if (string.IsNullOrEmpty(authCode))
            {
                throw new Exception("No authorization code received!");
            }

            if (authCode != null)
            {
                _oauthResult = await ExchangeCodeForTokenAsync(authCode);
                if (_oauthResult == null)
                {
                    return null;
                }
                else
                {
                    var res = await GetUsernameAsync(_oauthResult.AccessToken);
                    if (res != null)
                    {
                        _oauthResult.Username = res;
                        return _oauthResult;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Google Auth Flow Failed:{ex.Message}");
        }
        return null;
    }
    
    public async Task<GoogleToken?> RefreshTokenAsync(string refreshToken)
    {
        using var httpClient = new HttpClient();
        var content = new FormUrlEncodedContent(
            [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", ClientID),
        ]);

        var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tokenData = JsonDocument.Parse(responseData).RootElement;
        var newToken = new GoogleToken
        {
            AccessToken = tokenData.GetProperty("access_token").GetString()
                ?? throw new Exception("access_token missing in Googles reply"),
            RefreshToken = tokenData.TryGetProperty("refresh_token", out var refreshProp)
                ? refreshProp.GetString() ?? refreshToken // fallback till gamla om null
                : refreshToken,
            TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer",
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
            Scope = tokenData.GetProperty("scope").GetString() ?? string.Empty
        };
        return newToken;
    }

    private static string BuildAuthorizationUrl(string[] requestedScopes, string clientId, string redirectUri, string codeChallenge, string state)
    {
        // Scopes ska vara ett mellanslag-separerat string (inte komma-separerat)
        string scope = string.Join(" ", requestedScopes);

        var queryParams = new Dictionary<string, string>()
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["access_type"] = "offline",  // för att få refresh_token
            ["prompt"] = "consent"        // säkerställer att refresh_token alltid skickas
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{OAuthBaseUrl}?{queryString}";
    }

    private async Task<GoogleToken> ExchangeCodeForTokenAsync(string authCode)
    {
        if (string.IsNullOrEmpty(_currentCodeVerifier))
            throw new InvalidOperationException("Code verifier was null. Start auth flow first.");

        // client id finns som konstant i din klass (GoogleClientID)
        string clientId = ClientID;

        using var httpClient = new HttpClient();

        var requestFields = new[]
        {
        new KeyValuePair<string, string>("grant_type", "authorization_code"),
        new KeyValuePair<string, string>("code", authCode),
        new KeyValuePair<string, string>("redirect_uri", RedirectUri),
        new KeyValuePair<string, string>("client_id", clientId),
        new KeyValuePair<string, string>("code_verifier", _currentCodeVerifier)
    };

        try
        {
            var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(requestFields));
            var responseData = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Försök extrahera felmeddelande från Google-responsen för bättre debugging
                string errorMsg = responseData;
                try
                {
                    using var docErr = JsonDocument.Parse(responseData);
                    if (docErr.RootElement.TryGetProperty("error_description", out var ed)) errorMsg = ed.GetString() ?? errorMsg;
                    else if (docErr.RootElement.TryGetProperty("error", out var e)) errorMsg = e.GetString() ?? errorMsg;
                }
                catch { /* ignore parse errors */ }

                throw new Exception($"Token request failed: {response.StatusCode}\n{errorMsg}");
            }

            using var doc = JsonDocument.Parse(responseData);
            var root = doc.RootElement;

            string accessToken = root.GetProperty("access_token").GetString() ??
                                 throw new Exception("Missing access_token in token response");

            int expiresIn = root.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
            string refreshToken = root.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() ?? string.Empty : string.Empty;
            string tokenType = root.TryGetProperty("token_type", out var ttEl) ? ttEl.GetString() ?? "Bearer" : "Bearer";
            string scope = root.TryGetProperty("scope", out var sEl) ? sEl.GetString() ?? string.Empty : string.Empty;

            var result = new GoogleToken
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = tokenType,
                Scope = scope,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
            };

            // Hämta userinfo (e-post / sub) för att fylla Username (valfritt men ofta användbart)
            try
            {
                using var userReq = new HttpRequestMessage(HttpMethod.Get, UserinfoEndpoint);
                userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var userResp = await httpClient.SendAsync(userReq);

                if (userResp.IsSuccessStatusCode)
                {
                    var userJson = await userResp.Content.ReadAsStringAsync();
                    using var userDoc = JsonDocument.Parse(userJson);
                    var userRoot = userDoc.RootElement;

                    // Försök först få e-post, annars sub (subject)
                    if (userRoot.TryGetProperty("email", out var emailEl))
                        result.Username = emailEl.GetString() ?? string.Empty;
                    else if (userRoot.TryGetProperty("sub", out var subEl))
                        result.Username = subEl.GetString() ?? string.Empty;
                }
                // om userinfo misslyckas, så är det inte kritiskt — vi har åtminstone tokenen
            }
            catch
            {
                // ignorera userinfo-fel, returnera token ändå
            }

            // Spara token lokalt
            _stateService.SerializeToken<GoogleToken>(OAuthServices.Youtube, result);
            // Rensa temporära värden så de inte återanvänds
            _currentCodeVerifier = null;
            _currentState = null;

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error on token exchange: {ex.Message}", ex);
        }
    }

    private async Task<string?> GetUsernameAsync(string accessToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await httpClient.GetAsync(_userInfoUrl);
            var responseData = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = JsonDocument.Parse(responseData).RootElement;

            // Försök först med "email", annars fallback till "name", annars "sub"
            if (json.TryGetProperty("name", out var nameProp))
            {
                return nameProp.GetString() ?? throw new Exception("Name field is null");
            }
            else if (json.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString() ?? throw new Exception("Email field is null");
            }
            else if (json.TryGetProperty("sub", out var subProp))
            {
                return subProp.GetString() ?? throw new Exception("Sub field is null");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }
        return "Failed to get username";
    }

    private async Task<string> ListenForAuthCodeAsync()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var context = await listener.GetContextAsync().WaitAsync(cts.Token);

            // Validera state
            var receivedState = context.Request.QueryString["state"];

            if (receivedState != _currentState)
            {
                await SendBrowserResponse(context.Response,
                    "<html><body>Invalid state-parameter</body></html>");
                throw new Exception("State doesnt match - potential CSRF-attack");
            }

            string authCode = context.Request.QueryString["code"]!;
            await SendBrowserResponse(context.Response,
                "<html><body> Login successful. You may close this window.</body></html>");
            if (authCode != null)
            {
                return authCode;
            }
            else
            {
                throw new Exception("we did not receive an auth code");
            }
        }
        catch (OperationCanceledException)
        {
            throw new Exception("Login timed out");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task SendBrowserResponse(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to open browser: {ex.Message}");
        }
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new Random();
        return new string([.. Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)])]);
    }
    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
