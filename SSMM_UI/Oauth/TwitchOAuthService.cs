using SSMM_UI.Oauth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class TwitchOAuthService
{
    private const string OAuthBaseUrl = "https://id.twitch.tv/oauth2";
    private const string ApiBaseUrl = "https://api.twitch.tv/helix";
    private const string RedirectUri = "http://localhost:12345/";
    private const string TokenFilePath = "twitch_token.json";

    private string _currentState;

    public async Task<TwitchAuthResult> AuthenticateUserAsync(string[] requestedScopes)
    {
        if (File.Exists(TokenFilePath))
        {
            var res = LoadSavedToken();
            return res;
        }
        else
        {

            _currentState = GenerateRandomString(32);
            string scope = string.Join(" ", requestedScopes);

            string clientId = Environment.GetEnvironmentVariable("TwitchClientID") ?? throw new Exception("TwitchClientID not set");

            string authUrl = $"{OAuthBaseUrl}/authorize?" +
                             $"client_id={Uri.EscapeDataString(clientId)}&" +
                             $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                             $"response_type=code&" +
                             $"scope={Uri.EscapeDataString(scope)}&" +
                             $"state={_currentState}";

            OpenBrowser(authUrl);

            string authCode = await ListenForAuthCodeAsync();
            if (string.IsNullOrEmpty(authCode))
                throw new Exception("No authorization code received");

            var tokenResult = await ExchangeCodeForTokenAsync(authCode);
            tokenResult.Username = await GetUsernameAsync(tokenResult.AccessToken);
            SaveToken(tokenResult);
            return tokenResult;
        }
    }

    private async Task<TwitchAuthResult> ExchangeCodeForTokenAsync(string authCode)
    {
        string clientId = Environment.GetEnvironmentVariable("TwitchClientID") ?? throw new Exception("TwitchClientID not set");
        string clientSecret = Environment.GetEnvironmentVariable("TwitchClientSecret") ?? throw new Exception("TwitchClientSecret not set");

        using var httpClient = new HttpClient();

        var postBody = new Dictionary<string, string>
        {
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"code", authCode},
            {"grant_type", "authorization_code"},
            {"redirect_uri", RedirectUri}
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{OAuthBaseUrl}/token")
        {
            Content = new FormUrlEncodedContent(postBody)
        };

        var response = await httpClient.SendAsync(request);
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Twitch error ({response.StatusCode}): {responseData}");

        var tokenData = JsonDocument.Parse(responseData).RootElement;

        return new TwitchAuthResult
        {
            AccessToken = tokenData.GetProperty("access_token").GetString() ?? throw new Exception("Missing access_token"),
            RefreshToken = tokenData.GetProperty("refresh_token").GetString() ?? "",
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
            Scope = string.Join(" ", tokenData.GetProperty("scope").EnumerateArray().Select(s => s.GetString())),
            TokenType = tokenData.GetProperty("token_type").GetString() ?? "Bearer"
        };
    }

    private async Task<string> GetUsernameAsync(string accessToken)
    {
        string clientId = Environment.GetEnvironmentVariable("TwitchClientID") ?? throw new Exception("TwitchClientID not set");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);

        var response = await httpClient.GetAsync($"{ApiBaseUrl}/users");
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"User info failed: {response.StatusCode}\n{responseData}");

        var json = JsonDocument.Parse(responseData).RootElement;
        var user = json.GetProperty("data")[0];

        return user.TryGetProperty("login", out var login)
            ? login.GetString()
            : user.GetProperty("display_name").GetString();
    }

    private async Task<string> ListenForAuthCodeAsync()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var context = await listener.GetContextAsync().WaitAsync(cts.Token);

        if (context.Request.QueryString["state"] != _currentState)
        {
            await SendBrowserResponse(context.Response, "<html><body>❌ Invalid state</body></html>");
            throw new Exception("State mismatch");
        }

        string code = context.Request.QueryString["code"];
        await SendBrowserResponse(context.Response, "<html><body>✅ Login successful</body></html>");
        return code;
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private async Task SendBrowserResponse(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    public void SaveToken(TwitchAuthResult token)
    {
        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TokenFilePath, json);
    }

    public TwitchAuthResult LoadSavedToken()
    {
        if (!File.Exists(TokenFilePath)) return null;

        try
        {
            var json = File.ReadAllText(TokenFilePath);
            return JsonSerializer.Deserialize<TwitchAuthResult>(json);
        }
        catch
        {
            return null;
        }
    }
}

