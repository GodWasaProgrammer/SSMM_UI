using SSMM_UI.Oauth;
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

    private string _currentCodeVerifier;
    private string _currentState;

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

            Console.WriteLine($"Öppnar auktoriserings-URL: {authUrl}");

            // 3. Öppna webbläsare
            OpenBrowser(authUrl);

            // 4. Lyssna efter callback
            string authCode = await ListenForAuthCodeAsync();
            if (string.IsNullOrEmpty(authCode))
            {
                throw new Exception("Ingen auktoriseringskod mottagen");
            }

            // 5. Utför tokenutbyte
            var tokenResult = await ExchangeCodeForTokenAsync(authCode);
            if (tokenResult == null)
            {
                throw new Exception("Tokenutbyte misslyckades");
            }

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

            return authCode;
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
        string clientId = Environment.GetEnvironmentVariable("KickClientID") ??
                        throw new Exception("ClientID not set");
        string clientSecret = Environment.GetEnvironmentVariable("KickClientSecret") ??
                           throw new Exception("ClientSecret not set");

        using var httpClient = new HttpClient();
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code_verifier", _currentCodeVerifier)
        });

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

    private async Task<string> GetUsernameAsync(string accessToken)
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

        throw new Exception("No user data returned or malformed response");
    }


    private (string codeVerifier, string codeChallenge) GeneratePkceParameters()
    {
        var codeVerifier = GenerateRandomString(128);
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);
        return (codeVerifier, codeChallenge);
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
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

    private async Task SendBrowserResponse(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private void SaveToken(KickAuthResult token)
    {
        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TokenFilePath, json);
    }
}
