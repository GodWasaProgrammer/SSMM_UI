using HtmlAgilityPack;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
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

namespace SSMM_UI.Oauth.Kick;

public class KickAuthService : IOAuthService<KickToken>
{
    private const string OAuthBaseUrl = "https://id.kick.com";
    private const string RedirectUri = "http://localhost:12345/callback/";
    private const string ClientID = "01K1N4MW57X4G7Q50G7ZS6CA9Y";
    private KickToken? _kickAuthResult;
    private readonly StateService _stateService;

    public static string GetClientId() => ClientID;
    public string GetAccessToken() => _kickAuthResult?.AccessToken ?? string.Empty;

    private static readonly string[] DefaultScopes =
    [
        "user:read",
        "channel:read",
        "channel:write",
        "chat:write"
    ];

    public static string Combine(params string[] scopes)
    {
        return string.Join(" ", scopes);
    }

    private string? _currentCodeVerifier;
    private string? _currentState;

    private readonly ILogService _logger;

    public KickAuthService(ILogService logger, StateService stateService)
    {
        _logger = logger;
        _stateService = stateService;
    }

    public async Task<KickToken?> LoginAsync()
    {
        var token = await TryUseExistingTokenAsync();
        if (token != null)
        {
            if (token.IsValid)
            {
                _kickAuthResult = token;
                return token;
            }
        }
        try
        {
            // 1. Generera PKCE-parametrar
            _currentCodeVerifier = PKCEHelper.GenerateCodeVerifier();
            var codeChallenge = PKCEHelper.GenerateCodeChallenge(_currentCodeVerifier);
            _currentState = PKCEHelper.RandomString(32);
            string authUrl = BuildAuthUrl(codeChallenge);

            // 3. Öppna webbläsare
            BrowserHelper.OpenUrlInBrowser(authUrl);

            // 4. Lyssna efter callback
            var CallBackResult = await OAuthListenerHelper.WaitForCallbackAsync(RedirectUri, _currentState, default);
            string? authCode = CallBackResult?["code"];
            if (string.IsNullOrEmpty(authCode)) throw new Exception("No authorization code received");

            // 5. Utför tokenutbyte
            var tokenResult = await ExchangeCodeForTokenAsync(authCode) ?? throw new Exception("Token exchange failed");

            // 6. Hämta användarinformation
            tokenResult.Username = await GetUsernameAsync(tokenResult.AccessToken);

            // 7. Spara token
            _stateService.SerializeToken(OAuthServices.Kick, tokenResult);

            _kickAuthResult = tokenResult;
            return tokenResult;
        }
        finally
        {
            // Rensa känsliga data
            _currentCodeVerifier = null;
            _currentState = null;
        }
    }

    private string BuildAuthUrl(string codeChallenge)
    {
        // 2. Bygg auktoriserings-URL
        string scope = Combine(DefaultScopes);
        string authUrl = $"{OAuthBaseUrl}/oauth/authorize?" +
                       $"response_type=code&" +
                       $"client_id={Uri.EscapeDataString(ClientID)}&" +
                       $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                       $"scope={Uri.EscapeDataString(scope)}&" +
                       $"code_challenge={codeChallenge}&" +
                       $"code_challenge_method=S256&" +
                       $"state={_currentState}";
        return authUrl;
    }

    public async Task<KickToken?> RefreshTokenAsync(string refreshToken)
    {
        using var httpClient = new HttpClient();
        var content = new FormUrlEncodedContent(
        [
        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        new KeyValuePair<string, string>("refresh_token", refreshToken),
        new KeyValuePair<string, string>("client_id", ClientID),
    ]);

        var response = await httpClient.PostAsync($"{OAuthBaseUrl}/oauth/token", content);
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Refresh-token failed: {response.StatusCode}\n{responseData}");
            return null;
        }


        var tokenData = JsonDocument.Parse(responseData).RootElement;
        var newToken = new KickToken
        {
            AccessToken = tokenData.GetProperty("access_token").GetString()!,
            RefreshToken = tokenData.GetProperty("refresh_token").GetString()!,
            TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer",
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
            Scope = tokenData.GetProperty("scope").GetString() ?? ""
        };

        _stateService.SerializeToken(OAuthServices.Kick, newToken);
        return newToken;
    }
    public async Task<KickToken?> TryUseExistingTokenAsync()
    {
        var token = _stateService.DeserializeToken<KickToken>(OAuthServices.Kick);
        if (token != null)
        {
            if (token.IsValid)
            { return token; }
            else if (!string.IsNullOrEmpty(token.RefreshToken))
            {
                var newToken = await RefreshTokenAsync(token.RefreshToken);
                if (newToken != null)
                {
                    if (newToken.IsValid)
                    {
                        return newToken;
                    }
                }
            }
            else
            {
                _logger.Log("Existing Kick token is invalid and no refresh token is available.");
                return null;
            }
        }
        return null;
    }

    private async Task<KickToken> ExchangeCodeForTokenAsync(string authCode)
    {
        if (_currentCodeVerifier != null)
        {


            string clientSecret = Environment.GetEnvironmentVariable("KickClientSecret") ??
                               throw new Exception("ClientSecret not set");

            using var httpClient = new HttpClient();
            FormUrlEncodedContent tokenRequest = new(
            [
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            new KeyValuePair<string, string>("client_id", ClientID),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code_verifier", _currentCodeVerifier)
            ]);

            try
            {
                var response = await httpClient.PostAsync($"{OAuthBaseUrl}/oauth/token", tokenRequest);
                var responseData = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Token request failed: {response.StatusCode}\n{responseData}");
                }

                var tokenData = JsonDocument.Parse(responseData).RootElement;
                return new KickToken
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString() ??
                                throw new Exception("Missing access_token in response"),
                    RefreshToken = tokenData.GetProperty("refresh_token").GetString() ??
                                 throw new Exception("Missing refresh_token in response"),
                    TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer",
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
                    Scope = tokenData.GetProperty("scope").GetString() ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during token exchange: {ex.Message}");
            }
        }
        else
        {
            throw new InvalidOperationException("Codeverifier was null");
        }
    }

    private async Task<string> GetUsernameAsync(string accessToken)
    {
        try
        {

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await httpClient.GetAsync("https://api.kick.com/public/v1/users");
            var responseData = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"User info request failed: {response.StatusCode}\n{responseData}");
            }

            var json = JsonDocument.Parse(responseData).RootElement;

            if (json.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array && dataArray.GetArrayLength() > 0)
            {
                var firstUser = dataArray[0];
                if (firstUser.TryGetProperty("name", out var nameProp))
                {
                    return nameProp.GetString() ?? throw new Exception("Name field is null");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }

        return ("No user data returned or malformed response");
    }
}
