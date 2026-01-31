using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth.X;

public class XAuthService : IOAuthService<XToken>
{
    private readonly string _clientId = "TGVNbDAzN0hOY1JLNlBSeVg3ZmU6MTpjaQ";
    private readonly string _redirectUri = "http://localhost:7890/callback";
    private readonly HttpClient _http = new();
    private readonly StateService _stateService;
    private readonly ILogService _logger;
    private readonly string[] _scopes = [
                "tweet.read",
                "tweet.write",
                "users.read",
                "offline.access"
            ];

    public XAuthService(ILogService logger, StateService stateservice)
    {
        _stateService = stateservice;
        _logger = logger;
    }

    public async Task<XToken?> TryUseExistingTokenAsync()
    {
        var token = _stateService.DeserializeToken<XToken>(AuthProvider.X);

        if (token == null)
        {
            return null;
        }
        if (token.IsValid) 
        {
            var duser = await GetCurrentUserAsync(token);
            token.Username = duser?.Username;
            return token; 
        }
        if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            var refreshed = await RefreshTokenAsync(token.RefreshToken);
            if (refreshed != null)
            {
                token = refreshed;
                // omit refresh token?
                _stateService.SerializeToken(AuthProvider.X, token);
                return token;
            }
        }

        return null;
    }

    public async Task<XToken?> LoginAsync()
    {
        var token = await TryUseExistingTokenAsync();
        if (token != null)
            return token;

        _logger.Log("No existing X token found, starting authorization...");

        token = await AuthorizeWithPkceAsync(60);
        return token;
    }

    public async Task<XToken?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

        var tokenEndpoint = "https://api.twitter.com/2/oauth2/token";

        // Form data för refresh
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _clientId,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return null;

        try
        {
            // Deserialisera till XToken
            var refreshedToken = JsonSerializer.Deserialize<XToken>(body) ?? throw new Exception("Failed to deserialize refreshed token.");

            // Hämta användarinformation direkt efter refresh
            //var user = await GetCurrentUserAsync(refreshedToken);
            //refreshedToken.Username = user?.Username;

            return refreshedToken;
        }
        catch (Exception ex)
        {
            _logger.Log($"Exception in Token refresh for X:{ex.Message}");
        }
        return null;
    }

    // -------------------------
    // PUBLIC: OAuth2 PKCE Flow
    // -------------------------
    /// <summary>
    /// Starts the OAuth2 PKCE authorization flow.
    /// </summary>
    public async Task<XToken> AuthorizeWithPkceAsync(int timeoutSeconds = 120)
    {
        // 1) skapa code_verifier & code_challenge
        var codeVerifier = PKCEHelper.GenerateCodeVerifier();
        var codeChallenge = PKCEHelper.GenerateCodeChallenge(codeVerifier);

        // 2) bygg authorize URL
        var state = PKCEHelper.RandomString(32);
        var scopeStr = HttpUtility.UrlEncode(string.Join(" ", _scopes));
        string authUrl = BuildAuthUrl(codeChallenge, state, scopeStr);

        // 3) öppna browser och vänta på callback
        BrowserHelper.OpenUrlInBrowser(authUrl);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var callback = await OAuthListenerHelper.WaitForCallbackAsync(_redirectUri, state, cts.Token);

        if (callback == null) return null!;

        if (!callback.TryGetValue("code", out var code)) return null!;

        // 4) exchange code for token
        var JsonToken = await ExchangePkceCodeForTokenAsync(code!, codeVerifier);
        XToken xToken = new();

        if (JsonToken != null)
        {

            if (JsonToken.RootElement.TryGetProperty("access_token", out var accessTokenProp))
                xToken.AccessToken = accessTokenProp.GetString() ?? "";
            else
                xToken.AccessToken = "";


            xToken.RefreshToken = JsonToken.RootElement.GetProperty("refresh_token").GetString() ?? "";
            xToken.ExpiresIn = JsonToken.RootElement.GetProperty("expires_in").GetInt32();
            xToken.TokenType = JsonToken.RootElement.GetProperty("token_type").GetString() ?? "";
            xToken.Scope = JsonToken.RootElement.GetProperty("scope").GetString() ?? "";
        }

        var user = await GetCurrentUserAsync(xToken);

        xToken.Username = user?.Username ?? "";
        _stateService.SerializeToken(AuthProvider.X, xToken);

        return xToken;
    }

    private string BuildAuthUrl(string codeChallenge, string state, string scopeStr)
    {
        return $"https://twitter.com/i/oauth2/authorize?response_type=code&client_id={_clientId}&redirect_uri={HttpUtility.UrlEncode(_redirectUri)}" +
                    $"&scope={scopeStr}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256";
    }

    public async Task<XUser?> GetCurrentUserAsync(XToken token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var resp = await http.GetAsync("https://api.x.com/2/users/me");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            _logger.Log($"Failed to fetch user info: {resp.StatusCode} - {body}");

        try
        {

            var json = JsonDocument.Parse(body);
            var user = json.RootElement.GetProperty("data");

            return new XUser
            {
                Id = user.GetProperty("id").GetString()!,
                Username = user.GetProperty("username").GetString()!,
                Name = user.GetProperty("name").GetString()!
            };
        }
        catch (Exception ex)
        {
            _logger.Log($"Exception in GetCurrentUserAsync for X: {ex.Message}");
            return null;
        }
    }
    private async Task<JsonDocument?> ExchangePkceCodeForTokenAsync(string code, string codeVerifier)
    {
        var tokenEndpoint = "https://api.twitter.com/2/oauth2/token";

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["code_verifier"] = codeVerifier
        };

        var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Token exchange failed: {resp.StatusCode} - {body}");

        return JsonDocument.Parse(body);
    }

    // -------------------------
    // Inner helper classes
    // -------------------------
    public class UriQuery
    {
        public Uri Url { get; }
        public IReadOnlyDictionary<string, string?> Query { get; }
        public UriQuery(Uri url, IReadOnlyDictionary<string, string?> query)
        {
            Url = url;
            Query = query;
        }
    }
}