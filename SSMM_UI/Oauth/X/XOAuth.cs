using SSMM_UI.API_Key_Secrets_Loader;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth.X;

public class XOAuth
{
    private readonly string _clientId;       // Client ID / API Key
    private readonly string _redirectUri = "http://127.0.0.1:7890/callback";
    private readonly HttpClient _http = new();
    private StateService _stateService;
    private ILogService _logger;

    public XOAuth(ILogService logger, StateService stateservice)
    {
        _clientId = "TGVNbDAzN0hOY1JLNlBSeVg3ZmU6MTpjaQ";
        _stateService = stateservice;
        _logger = logger;
    }

    public async Task<XToken> AuthenticateOrRefreshAsync()
    {
        var token = _stateService.DeserializeToken<XToken>(Enums.OAuthServices.X);
        if (token == null)
        {
            _logger.Log("No existing X token found, starting authorization...");
            var scopes = new[] {
                "tweet.read",
                "tweet.write",   // ← krävs för POST /2/tweets och DELETE /2/tweets/:id
                "users.read",
                "offline.access" // ← (valfritt) för att få en refresh_token
            };
            token = await AuthorizeWithPkceAsync(scopes, 60);
            if (token == null)
            {
                throw new Exception("X OAuth authorization failed.");
            }
            return token;
        }
        else if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            _logger.Log("Refreshing X access token...");
            token = await RefreshTokenAsync(token.RefreshToken, _clientId);
            _stateService.SerializeToken(Enums.OAuthServices.X, token);
            return token;
        }
        else
        {
            throw new Exception("X OAuth token expired and no refresh token available.");
        }
    }

    /// <summary>
    /// Uppdaterar access-token med hjälp av refresh-token (PKCE flow).
    /// </summary>
    /// <param name="refreshToken">Refresh token som du tidigare fått från X.</param>
    /// <param name="clientId">Din OAuth 2.0 Client ID.</param>
    /// <returns>Ny XToken med uppdaterad access-token och expiry.</returns>
    public async Task<XToken> RefreshTokenAsync(string refreshToken, string clientId)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

        var tokenEndpoint = "https://api.twitter.com/2/oauth2/token";

        // Form data för refresh
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Token refresh failed: {resp.StatusCode}\n{body}");

        // Deserialisera till XToken
        var refreshedToken = JsonSerializer.Deserialize<XToken>(body);
        if (refreshedToken == null)
            throw new Exception("Failed to deserialize refreshed token.");

        refreshedToken.CreatedAt = DateTimeOffset.UtcNow;

        // Hämta användarinformation direkt efter refresh
        var user = await GetCurrentUserAsync(refreshedToken);
        refreshedToken.Username = user?.Username;

        return refreshedToken;
    }

    // -------------------------
    // PUBLIC: OAuth2 PKCE Flow
    // -------------------------
    /// <summary>
    /// Startar OAuth2 Authorization Code flow med PKCE. Returnerar access_token (JSON) inkl. refresh_token om tillgängligt.
    /// </summary>
    public async Task<XToken> AuthorizeWithPkceAsync(IEnumerable<string> scopes, int timeoutSeconds = 120)
    {
        // 1) skapa code_verifier & code_challenge
        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);

        // 2) bygg authorize URL
        var state = RandomString(32);
        var scopeStr = HttpUtility.UrlEncode(string.Join(" ", scopes));
        var authUrl =
            $"https://twitter.com/i/oauth2/authorize?response_type=code&client_id={_clientId}&redirect_uri={HttpUtility.UrlEncode(_redirectUri)}" +
            $"&scope={scopeStr}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256";

        // 3) öppna browser och vänta på callback
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var callback = await LaunchBrowserAndWaitForCallbackAsync(authUrl, state, cts.Token);

        if (callback == null) return null;

        if (!callback.Query.TryGetValue("code", out var code)) return null;

        // 4) exchange code for token
        var token = await ExchangePkceCodeForTokenAsync(code!, codeVerifier);
        XToken xToken = new XToken();
        xToken.AccessToken = token.RootElement.GetProperty("access_token").GetString() ?? "";
        xToken.RefreshToken = token.RootElement.GetProperty("refresh_token").GetString() ?? "";
        xToken.ExpiresIn = token.RootElement.GetProperty("expires_in").GetInt32();
        xToken.TokenType = token.RootElement.GetProperty("token_type").GetString() ?? "";
        xToken.Scope = token.RootElement.GetProperty("scope").GetString() ?? "";

        var user = await GetCurrentUserAsync(xToken);

        xToken.Username = user?.Username ?? "";
        _stateService.SerializeToken(Enums.OAuthServices.X, xToken);

        return xToken;
    }

    public async Task<XUser?> GetCurrentUserAsync(XToken token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var resp = await http.GetAsync("https://api.x.com/2/users/me");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch user info: {resp.StatusCode} - {body}");

        var json = System.Text.Json.JsonDocument.Parse(body);
        var user = json.RootElement.GetProperty("data");

        return new XUser
        {
            Id = user.GetProperty("id").GetString()!,
            Username = user.GetProperty("username").GetString()!,
            Name = user.GetProperty("name").GetString()!
        };
    }

    public class XUser
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"{Name} (@{Username})";
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
    // Helper: Launch browser + local HTTP listener for redirect
    // -------------------------
    private async Task<UriQuery?> LaunchBrowserAndWaitForCallbackAsync(string url, string? expectedState, CancellationToken ct)
    {
        // Starta enkel HttpListener
        var uri = new Uri(_redirectUri);
        var prefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        // öppna webbläsare
        OpenBrowser(url);

        try
        {
            var contextTask = listener.GetContextAsync();
            using (ct.Register(() => listener.Stop()))
            {
                var context = await contextTask.ConfigureAwait(false); // may throw if stopped
                var req = context.Request;
                var resp = context.Response;

                // skriv enkel HTML-sida till användaren
                var responseString = "<html><body><h2>Du kan nu stänga detta fönster.</h2></body></html>";
                var buffer = Encoding.UTF8.GetBytes(responseString);
                resp.ContentLength64 = buffer.Length;
                resp.OutputStream.Write(buffer, 0, buffer.Length);
                resp.OutputStream.Close();

                // kontrollera state om angivet
                var q = HttpUtility.ParseQueryString(req.Url.Query);
                var queryDict = new Dictionary<string, string?>();
                foreach (var k in q.AllKeys)
                    if (k != null) queryDict[k] = q[k];

                if (expectedState != null)
                {
                    if (!queryDict.TryGetValue("state", out var returnedState) || returnedState != expectedState)
                    {
                        throw new Exception("State mismatch in OAuth callback");
                    }
                }

                return new UriQuery(req.Url, queryDict);
            }
        }
        catch (HttpListenerException)
        {
            return null;
        }
        finally
        {
            if (listener.IsListening) listener.Stop();
        }
    }

    // -------------------------
    // Helper: Browser
    // -------------------------
    private static void OpenBrowser(string url)
    {
        try
        {
            // cross-platform way
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // fallback for older .NET / OS combos
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            else if (OperatingSystem.IsLinux()) Process.Start("xdg-open", url);
            else if (OperatingSystem.IsMacOS()) Process.Start("open", url);
            else throw;
        }
    }

    // -------------------------
    // Utils: PKCE helpers
    // -------------------------
    private static string CreateCodeVerifier()
    {
        var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = sha.ComputeHash(bytes);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string RandomString(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    // -------------------------
    // Utils: Query parsing
    // -------------------------
    private static Dictionary<string, string> ParseQueryString(string qs)
    {
        var d = new Dictionary<string, string>();
        var parts = qs.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var kv = p.Split('=', 2);
            var k = HttpUtility.UrlDecode(kv[0]);
            var v = kv.Length > 1 ? HttpUtility.UrlDecode(kv[1]) : "";
            d[k] = v;
        }
        return d;
    }

    // -------------------------
    // Simple encrypted storage (Windows DPAPI)
    // -------------------------
    public static void ProtectAndSave(string path, string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        System.IO.File.WriteAllBytes(path, protectedData);
    }

    public static string? LoadAndUnprotect(string path)
    {
        if (!System.IO.File.Exists(path)) return null;
        var protectedData = System.IO.File.ReadAllBytes(path);
        var data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }

    // -------------------------
    // Inner helper classes
    // -------------------------
    private class UriQuery
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