using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
public class KickOAuthService
{
    private const string OAuthBaseUrl = "https://id.kick.com";
    private const string ApiBaseUrl = "https://api.kick.com";
    private const string RedirectUri = "http://localhost:12345/callback/";
    private const string TokenFilePath = "kick_token.json";

    // Klass för scope-hantering
    public static class Scopes
    {
        public const string UserRead = "user:read";
        public const string ChannelRead = "channel:read";
        public const string ChannelWrite = "channel:write";
        public const string ChatWrite = "chat:write";

        public static string Combine(params string[] scopes)
        {
            return string.Join(" ", scopes);
        }
    }

    private string? _currentCodeVerifier;
    private string? _currentState;

    private ILogService _logger;
    public KickOAuthService(ILogService logger)
    {
        _logger = logger;
    }

    public async Task<KickAuthResult> AuthenticateUserAsync(string[] requestedScopes)
    {
        try
        {
            // 1. Generera PKCE-parametrar
            var (codeVerifier, codeChallenge) = GeneratePkceParameters();
            _currentCodeVerifier = codeVerifier;
            _currentState = GenerateRandomString(32);

            // 2. Bygg auktoriserings-URL
            string scope = Scopes.Combine(requestedScopes);
            string authUrl = $"{OAuthBaseUrl}/oauth/authorize?" +
                           $"response_type=code&" +
                           $"client_id={Uri.EscapeDataString(Environment.GetEnvironmentVariable("KickClientID") ?? throw new Exception("ClientID not set"))}&" +
                           $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                           $"scope={Uri.EscapeDataString(scope)}&" +
                           $"code_challenge={codeChallenge}&" +
                           $"code_challenge_method=S256&" +
                           $"state={_currentState}";

            _logger.Log($"Öppnar auktoriserings-URL: {authUrl}");

            // 3. Öppna webbläsare
            OpenBrowser(authUrl);

            // 4. Lyssna efter callback
            string authCode = await ListenForAuthCodeAsync();
            if (string.IsNullOrEmpty(authCode))
            {
                throw new Exception("Ingen auktoriseringskod mottagen");
            }

            // 5. Utför tokenutbyte
            var tokenResult = await ExchangeCodeForTokenAsync(authCode) ?? throw new Exception("Tokenutbyte misslyckades");

            // 6. Hämta användarinformation
            tokenResult.Username = await GetUsernameAsync(tokenResult.AccessToken);

            // 7. Spara token
            SaveToken(tokenResult);

            return tokenResult;
        }
        finally
        {
            // Rensa känsliga data
            _currentCodeVerifier = null;
            _currentState = null;
        }
    }

    private static async Task<KickAuthResult> RefreshTokenAsync(string refreshToken)
    {
        string clientId = Environment.GetEnvironmentVariable("KickClientID")!;
        string clientSecret = Environment.GetEnvironmentVariable("KickClientSecret")!;

        using var httpClient = new HttpClient();
        var content = new FormUrlEncodedContent(
        [
        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        new KeyValuePair<string, string>("refresh_token", refreshToken),
        new KeyValuePair<string, string>("client_id", clientId),
        new KeyValuePair<string, string>("client_secret", clientSecret),
    ]);

        var response = await httpClient.PostAsync($"{OAuthBaseUrl}/oauth/token", content);
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Refresh-token misslyckades: {response.StatusCode}\n{responseData}");

        var tokenData = JsonDocument.Parse(responseData).RootElement;
        var newToken = new KickAuthResult
        {
            AccessToken = tokenData.GetProperty("access_token").GetString()!,
            RefreshToken = tokenData.GetProperty("refresh_token").GetString()!,
            TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer",
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
            Scope = tokenData.GetProperty("scope").GetString() ?? ""
        };

        SaveToken(newToken);
        return newToken;
    }

    public async Task<KickAuthResult?> IfTokenIsValidLoginAuto()
    {
        if (!File.Exists(TokenFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(TokenFilePath);
            var token = JsonSerializer.Deserialize<KickAuthResult>(json);

            if (token == null)
            {
                return null;
            }

            if (DateTime.UtcNow > token.ExpiresAt)
            {
                // Token har gått ut, försök förnya
                await RefreshTokenAsync(token.RefreshToken);
                token.Username = await GetUsernameAsync(token.AccessToken);
                return token;
            }

            // Token är fortfarande giltig
            token.Username = await GetUsernameAsync(token.AccessToken);
            return token;
        }
        catch (Exception ex)
        {
            // Logga eller hantera fel (t.ex. korrupt fil)
            _logger.Log($"❌ Fel vid autologin: {ex.Message}");
            return null;
        }
    }

    public async Task<KickAuthResult> AuthenticateOrRefreshAsync(string[] scopes)
    {
        if (File.Exists(TokenFilePath))
        {
            var json = File.ReadAllText(TokenFilePath);
            var token = JsonSerializer.Deserialize<KickAuthResult>(json);

            if (token != null)
            {
                if (DateTime.UtcNow > token.ExpiresAt)
                {
                    return await AuthenticateUserAsync(scopes);
                }
                var res = await GetUsernameAsync(token.AccessToken);
                token.Username = res;
            }

            if (token != null && token.ExpiresAt > DateTime.UtcNow)
            {

                return token; // Token fortfarande giltig
            }
            else if (token?.RefreshToken != null)
            {
                return await RefreshTokenAsync(token.RefreshToken); // Förnya token
            }
        }

        return await AuthenticateUserAsync(scopes); // Full login om inget funkar
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
                    "<html><body>❌ Ogiltig state-parameter</body></html>");
                throw new Exception("State matchar inte - potentiell CSRF-attack");
            }

            string authCode = context.Request.QueryString["code"];
            await SendBrowserResponse(context.Response,
                "<html><body>✅ Inloggning lyckades. Stäng detta fönster.</body></html>");
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
            throw new Exception("Inloggningstiden utgick");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task<KickAuthResult> ExchangeCodeForTokenAsync(string authCode)
    {
        if (_currentCodeVerifier != null)
        {

            string clientId = Environment.GetEnvironmentVariable("KickClientID") ??
                            throw new Exception("ClientID not set");
            string clientSecret = Environment.GetEnvironmentVariable("KickClientSecret") ??
                               throw new Exception("ClientSecret not set");

            using var httpClient = new HttpClient();
            FormUrlEncodedContent tokenRequest = new(
            [
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code_verifier", _currentCodeVerifier)
            ]);

            try
            {
                var response = await httpClient.PostAsync($"{OAuthBaseUrl}/oauth/token", tokenRequest);
                var responseData = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Tokenförfrågan misslyckades: {response.StatusCode}\n{responseData}");
                }

                var tokenData = JsonDocument.Parse(responseData).RootElement;
                return new KickAuthResult
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString() ??
                                throw new Exception("Saknar access_token i svar"),
                    RefreshToken = tokenData.GetProperty("refresh_token").GetString() ??
                                 throw new Exception("Saknar refresh_token i svar"),
                    TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer",
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
                    Scope = tokenData.GetProperty("scope").GetString() ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Fel vid tokenutbyte: {ex.Message}");
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

    private static (string codeVerifier, string codeChallenge) GeneratePkceParameters()
    {
        var codeVerifier = GenerateRandomString(128);
        var challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);
        return (codeVerifier, codeChallenge);
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
            throw new Exception($"Kunde inte öppna webbläsare: {ex.Message}");
        }
    }

    private static async Task SendBrowserResponse(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static void SaveToken(KickAuthResult token)
    {
        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TokenFilePath, json);
    }
}
