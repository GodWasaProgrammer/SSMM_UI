using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SSMM_UI.Interfaces;
using SSMM_UI.Oauth.X;
using SSMM_UI.Poster;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth.Facebook;

public class FacebookAuthService
{
    private readonly HttpClient _http = new();
    private readonly string _clientId = "1684960759068438";
    private readonly string _redirectUri = "http://localhost:7891/callback";

    private const string AuthEndpoint = "https://www.facebook.com/v21.0/dialog/oauth";
    private const string TokenEndpoint = "https://graph.facebook.com/v21.0/oauth/access_token";
    private const string UserInfoEndpoint = "https://graph.facebook.com/me?fields=id,name,email,picture";
    private ILogService? _logger;
    private StateService _stateService;

    private static readonly string[] _scopes =
    {
        "public_profile",
        "pages_show_list",
        "pages_read_engagement",
        "pages_manage_posts"
    };

    public FacebookAuthService(ILogService logger, StateService stateservice)
    {
        _logger = logger;
        _stateService = stateservice;
    }

    public async Task<List<FacebookPage>> RequestPagesAsync(FacebookToken userToken)
    {
        var pages = new List<FacebookPage>();

        try
        {
            if (string.IsNullOrEmpty(userToken.AccessToken))
                throw new Exception("Missing user access token.");

            using var http = new HttpClient();
            var url = $"https://graph.facebook.com/v21.0/me/accounts?access_token={userToken.AccessToken}";

            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Facebook /me/accounts failed: {resp.StatusCode}\n{body}");

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");

            foreach (var page in data.EnumerateArray())
            {
                var pageInfo = new FacebookPage
                {
                    Id = page.GetProperty("id").GetString() ?? "",
                    Name = page.GetProperty("name").GetString() ?? "",
                    AccessToken = page.GetProperty("access_token").GetString() ?? ""
                };

                pages.Add(pageInfo);

                Console.WriteLine($"✅ Page: {pageInfo.Name} ({pageInfo.Id})");
            }

            if (pages.Count == 0)
                Console.WriteLine("⚠️ No Facebook pages found for this user. Make sure they are an admin.");

            return pages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ RequestPagesAsync failed: {ex.Message}");
            return pages;
        }
    }

    /// <summary>
    /// Only For Auto Login
    /// </summary>
    /// <returns></returns>
    public async Task<FacebookToken?> TryUseExistingTokenAsync()
    {
        var token = _stateService.DeserializeToken<FacebookToken>(Enums.OAuthServices.Facebook);

        if (token == null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            try
            {
                //_logger?.Log("Refreshing X access token");
                var refreshed = await RefreshTokenAsync(token);
                if (refreshed != null)
                {
                    _stateService.SerializeToken(Enums.OAuthServices.X, refreshed);

                    return refreshed;
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Refresh Failed{ex.Message}");
            }
        }
        var fbuser = await GetCurrentUserAsync(token);
        if (fbuser != null)
        {
            token.Username = fbuser.Name;
            return token;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Hög nivå login/metod: authenticate eller refresh token automatiskt.
    /// </summary>
    public async Task<FacebookToken> AuthenticateOrRefreshAsync()
    {
        var token = _stateService.DeserializeToken<FacebookToken>(Enums.OAuthServices.Facebook);

        if (token != null)
        {
            // Token finns → kolla giltighet
            if (!token.IsExpired)
            {
                _logger?.Log("Existing Facebook token is still valid.");
                var res = await GetCurrentUserAsync(token); // validera token
                if (res != null)
                {
                    token.Username = res.Name;
                }
                return token;
            }
            // Token gått ut → refresh
            if (!string.IsNullOrEmpty(token.AccessToken))
            {
                _logger?.Log("Refreshing Facebook token...");
                var refreshed = await RefreshTokenAsync(token);
                _stateService.SerializeToken(Enums.OAuthServices.Facebook, refreshed);
                return refreshed;
            }
        }
        _logger?.Log("No existing Facebook token, starting authorization...");

        // 1️⃣ Skapa code verifier/challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // 2️⃣ Generera auth-url
        var authUrl = GetAuthorizationUrl(codeChallenge);
        OpenBrowser(authUrl);

        // 3️⃣ Vänta på redirect med code (t.ex. lokal HTTP listener)
        var code = await WaitForCodeAsync();

        // 4️⃣ Byt code mot token
        token = await ExchangeCodeForTokenAsync(code, codeVerifier);
        _stateService.SerializeToken(Enums.OAuthServices.Facebook, token);

        return token;
    }

    /// <summary>
    /// Startar en enkel HTTP listener på redirectUri och väntar på "code" i querystring
    /// </summary>
    /// <summary>
    /// Startar en minimal Kestrel-server på localhost för att lyssna på OAuth-redirect.
    /// Returnerar koden från query-parametern "code".
    /// </summary>
    public static Task<string> WaitForCodeAsync(int port = 7891, string callbackPath = "/callback")
    {
        var tcs = new TaskCompletionSource<string>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");

        var app = builder.Build();

        app.MapGet(callbackPath, async (context) =>
        {
            try
            {
                var query = context.Request.Query;
                if (!query.ContainsKey("code"))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Missing code in query string");
                    return;
                }

                var code = query["code"];
                var responseHtml = "<html><body><h2>You may now close this window</h2></body></html>";

                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(responseHtml);

                tcs.TrySetResult(code);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                // Stäng ner servern efter första request
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // kort delay så response hinner skickas
                    await app.StopAsync();
                });
            }
        });

        _ = Task.Run(() =>
        {
            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private const int VerifierLength = 64;

    public static string GenerateCodeVerifier()
    {
        var bytes = new byte[VerifierLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = sha256.ComputeHash(bytes);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private void OpenBrowser(string url)
    {
        // Din implementering för att öppna webbläsare
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Genererar URL för användarautentisering (PKCE)
    /// </summary>
    public string GetAuthorizationUrl(string codeChallenge)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _clientId;
        query["redirect_uri"] = _redirectUri;
        query["response_type"] = "code";
        query["scope"] = string.Join(",", _scopes);
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = "S256";

        return $"{AuthEndpoint}?{query}";
    }

    /// <summary>
    /// Byter authorization code mot access token (utan client_secret)
    /// </summary>
    public async Task<FacebookToken> ExchangeCodeForTokenAsync(string code, string codeVerifier)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["code_verifier"] = codeVerifier,
            ["code"] = code
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Facebook token exchange failed: {resp.StatusCode}\n{body}");

        var token = JsonSerializer.Deserialize<FacebookToken>(body)
            ?? throw new Exception("Failed to parse Facebook token response.");

        token.CreatedAt = DateTimeOffset.UtcNow;
        token.IsLongLived = false;

        // 🔁 Försök byta till long-lived direkt
        var longLived = await TryExchangeForLongLivedTokenAsync(token);
        if (longLived != null)
            token = longLived;

        // 🔹 Hämta användarprofil
        var user = await GetCurrentUserAsync(token);
        token.Username = user?.Name;

        return token;
    }

    /// <summary>
    /// Byter short-lived token mot long-lived token (60 dagar)
    /// </summary>
    private async Task<FacebookToken?> TryExchangeForLongLivedTokenAsync(FacebookToken shortToken)
    {
        var uri = $"{TokenEndpoint}?grant_type=fb_exchange_token" +
                  $"&client_id={_clientId}" +
                  $"&fb_exchange_token={shortToken.AccessToken}";

        var resp = await _http.GetAsync(uri);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"⚠️ Could not exchange for long-lived token: {body}");
            return null;
        }

        var longToken = JsonSerializer.Deserialize<FacebookToken>(body);
        if (longToken == null)
            return null;

        longToken.CreatedAt = DateTimeOffset.UtcNow;
        longToken.IsLongLived = true;
        longToken.RefreshToken = shortToken.AccessToken; // Behåll för ev. ny exchange

        return longToken;
    }

    /// <summary>
    /// "Förnyar" long-lived token när den närmar sig utgång
    /// </summary>
    public async Task<FacebookToken> RefreshTokenAsync(FacebookToken existing)
    {
        if (string.IsNullOrEmpty(existing.AccessToken))
            throw new Exception("Missing access token for refresh.");

        var refreshed = await TryExchangeForLongLivedTokenAsync(existing);
        return refreshed ?? existing;
    }

    /// <summary>
    /// Hämtar användarinformation med access_token
    /// </summary>
    private async Task<FacebookUser?> GetCurrentUserAsync(FacebookToken token)
    {
        var uri = $"{UserInfoEndpoint}&access_token={token.AccessToken}";
        var resp = await _http.GetAsync(uri);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch Facebook user info: {resp.StatusCode}\n{body}");

        return JsonSerializer.Deserialize<FacebookUser>(body);
    }
}

public class FacebookPage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccessToken { get; set; } = "";
}