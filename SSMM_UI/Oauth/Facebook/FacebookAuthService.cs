using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace SSMM_UI.Oauth.Facebook;

public class FacebookAuthService : IOAuthService<FacebookToken>
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

    public async Task<FacebookToken?> TryUseExistingTokenAsync()
    {
        var token = _stateService.DeserializeToken<FacebookToken>(Enums.OAuthServices.Facebook);

        if (token == null)
        {
            return null;
        }
        var res = await RefreshTokenAsync(token);
        if (res != null)
        {
            token = res;
            var fbuser = await GetCurrentUserAsync(res);
            if (fbuser != null)
            {
                token.Username = fbuser.Name;
                _stateService.SerializeToken(Enums.OAuthServices.Facebook, token);
                return token;
            }
        }
        return null;
    }

    public async Task<FacebookToken?> LoginAsync()
    {
        var token = await TryUseExistingTokenAsync();
        if (token != null)
            return token;

        _logger?.Log("No existing Facebook token, starting authorization...");

        // 1️⃣ Skapa code verifier/challenge
        var codeVerifier = PKCEHelper.GenerateCodeVerifier();
        var codeChallenge = PKCEHelper.GenerateCodeChallenge(codeVerifier);

        // 2️⃣ Generera auth-url
        var authUrl = PKCEHelper.GetAuthorizationUrl(codeChallenge, _clientId, _redirectUri, _scopes, AuthEndpoint);
        BrowserHelper.OpenUrlInBrowser(authUrl);

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


    public async Task<FacebookToken?> RefreshTokenAsync(FacebookToken existing)
    {
        if (string.IsNullOrEmpty(existing.AccessToken))
            throw new Exception("Missing access token for refresh.");

        var refreshed = await TryExchangeForLongLivedTokenAsync(existing);
        return refreshed ?? existing;
    }

    private async Task<FacebookUser?> GetCurrentUserAsync(FacebookToken token)
    {
        var uri = $"{UserInfoEndpoint}&access_token={token.AccessToken}";
        var resp = await _http.GetAsync(uri);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch Facebook user info: {resp.StatusCode}\n{body}");

        return JsonSerializer.Deserialize<FacebookUser>(body);
    }

    public Task<FacebookToken?> RefreshTokenAsync(string token)
    {
        throw new NotImplementedException("Facebook User long life tokens");
    }
}