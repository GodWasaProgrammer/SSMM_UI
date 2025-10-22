using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth.X;

public class XAuthService : IOAuthService<XToken>
{
    private readonly string _clientId = "TGVNbDAzN0hOY1JLNlBSeVg3ZmU6MTpjaQ";     // Client ID / API Key
    private readonly string _redirectUri = "http://localhost:7890/callback";
    private readonly HttpClient _http = new();
    private StateService _stateService;
    private ILogService _logger;
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
        var token = _stateService.DeserializeToken<XToken>(Enums.OAuthServices.X);

        if (token == null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            var refreshed = await RefreshTokenAsync(token.RefreshToken);
            if (refreshed != null)
            {
                token = refreshed;
                // omit refresh token?
                _stateService.SerializeToken(Enums.OAuthServices.X, token);
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

        token = await AuthorizeWithPkceAsync(_scopes, 60);
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
            var refreshedToken = JsonSerializer.Deserialize<XToken>(body);
            if (refreshedToken == null)
                throw new Exception("Failed to deserialize refreshed token.");

            refreshedToken.CreatedAt = DateTimeOffset.UtcNow;

            // Hämta användarinformation direkt efter refresh
            var user = await GetCurrentUserAsync(refreshedToken);
            refreshedToken.Username = user?.Username;

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
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var resp = await http.GetAsync("https://api.x.com/2/users/me");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch user info: {resp.StatusCode} - {body}");

        var json = JsonDocument.Parse(body);
        var user = json.RootElement.GetProperty("data");

        return new XUser
        {
            Id = user.GetProperty("id").GetString()!,
            Username = user.GetProperty("username").GetString()!,
            Name = user.GetProperty("name").GetString()!
        };
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
    public async Task<UriQuery?> LaunchBrowserAndWaitForCallbackAsync(string url, string? expectedState, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<UriQuery?>();
        var uri = new Uri(_redirectUri);
        var port = uri.Port;
        var callbackPath = uri.AbsolutePath; // "/callback"

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");

        var app = builder.Build();

        app.MapGet(callbackPath, async context =>
        {
            try
            {
                var query = context.Request.Query;
                var queryDict = new Dictionary<string, string?>();
                foreach (var kv in query)
                {
                    queryDict[kv.Key] = kv.Value;
                }

                if (expectedState != null)
                {
                    if (!queryDict.TryGetValue("state", out var returnedState) || returnedState != expectedState)
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("State mismatch");
                        tcs.TrySetException(new Exception("State mismatch in OAuth callback"));
                        return;
                    }
                }

                // Skriv enkel HTML till användaren
                var responseHtml = "<html><body><h2>Du kan nu stänga detta fönster.</h2></body></html>";
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(responseHtml);

                var result = new UriQuery(new Uri(context.Request.GetEncodedUrl()), queryDict);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // ge webbläsaren tid att rendera
                    await app.StopAsync();
                });
            }
        });

        // Öppna webbläsaren
        OpenBrowser(url);

        // Kör Kestrel i bakgrund
        _ = Task.Run(async () =>
        {
            try
            {
                await app.RunAsync();
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return await tcs.Task;
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