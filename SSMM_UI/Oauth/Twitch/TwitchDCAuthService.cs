using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchDCAuthService
{
    private readonly HttpClient _httpClient;
    private const string DcfApiAdress = "https://id.twitch.tv/oauth2/device";
    private const string TokenAdress = "https://id.twitch.tv/oauth2/token";
    public readonly string _clientId = "y1cd8maguk5ob1m3lwvhdtupbj6pm3";
    private const string TokenFilePath = "twitch_tokenDCF.json";
    private const string ApiBaseUrl = "https://api.twitch.tv/helix";
    public TwitchTokenTokenResponse? AuthResult;
    string[] scopes = new[]
        {
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        };
    private ILogService _logger;
    public TwitchDCAuthService(ILogService logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public string GetClientId()
    {
        return _clientId;
    }

    public string GetAccessToken()
    {
        if (AuthResult == null)
        {
            return string.Empty;
        }
        else
        {
            return AuthResult.AccessToken;
        }
    }

    public async Task<TwitchDCResponse> StartDeviceCodeFlowAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DcfApiAdress);

        var content = new FormUrlEncodedContent(new[]
        {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("scope", string.Join(" ", scopes))
                });

        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TwitchDCResponse>();
        return result!;
    }

    public async Task<TwitchTokenTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
    {
        var content = new FormUrlEncodedContent(
        [
        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        new KeyValuePair<string, string>("refresh_token", refreshToken),
        new KeyValuePair<string, string>("client_id", _clientId)
    ]);

        var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"⚠️ Refresh failed: {response.StatusCode}\n{responseBody}");
            var errorToken = new TwitchTokenTokenResponse();
            errorToken.ErrorMessage = $"⚠️ Refresh failed: {response.StatusCode}\n{responseBody}";
            // since the refresh failed, the token nor its refreshtoken is no longer valid, delete local token to force full relog
            File.Delete(TokenFilePath);
            return errorToken;
        }

        var token = JsonSerializer.Deserialize<TwitchTokenTokenResponse>(responseBody);
        if (token is not null)
        {
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            token.UserName = await GetUsernameAsync(token.AccessToken);
            SaveToken(token);
        }

        return token!;
    }

    public async Task<TwitchTokenTokenResponse?> TryLoadValidOrRefreshTokenAsync()
    {
        var token = LoadSavedToken();
        if (token is not null)
        {
            AuthResult = token;
        }
        if (token == null) return null;

        if (token.IsValid)
            return token;

        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            _logger.Log("🔁 Försöker förnya åtkomsttoken med refresh_token...");
            var res = await RefreshAccessTokenAsync(token.RefreshToken);
            if (res.ErrorMessage == null)
            {
                token.UserName = await GetUsernameAsync(token.AccessToken);
            }
            else
            {
                token.ErrorMessage = res.ErrorMessage;
            }
            return token;
        }

        return null;
    }


    public async Task<TwitchTokenTokenResponse?> PollForTokenAsync(string deviceCode, int intervalSeconds, int maxWaitSeconds = 300)
    {
        int elapsed = 0;

        while (elapsed < maxWaitSeconds)
        {
            // Vänta enligt det rekommenderade intervallet (sekunder -> millisekunder)
            await Task.Delay(intervalSeconds * 1000);
            elapsed += intervalSeconds;

            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("device_code", deviceCode),
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
        });

            var response = await _httpClient.PostAsync(TokenAdress, content);

            if (response.IsSuccessStatusCode)
            {
                var token = await response.Content.ReadFromJsonAsync<TwitchTokenTokenResponse>();

                if (token != null)
                {
                    token.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                    token.UserName = await GetUsernameAsync(token.AccessToken);
                    token.UserId = await GetUserIdAsync(token.AccessToken);
                    AuthResult = token;
                    SaveToken(token);
                    return token;
                }
            }

            // Läser felmeddelandet som sträng (för debug/logg)
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.Log($"Polling error ({response.StatusCode}): {errorBody}");

            try
            {
                var error = JsonSerializer.Deserialize<TwitchOAuthErrorResponse>(errorBody);

                switch (error?.Error)
                {
                    case "authorization_pending":
                        // Användaren har inte godkänt ännu – fortsätt vänta
                        continue;

                    case "slow_down":
                        // Twitch säger att vi pollar för snabbt – öka intervallet
                        intervalSeconds += 5;
                        _logger.Log("Twitch säger att vi pollar för snabbt, ökar väntetiden...");
                        continue;

                    case "access_denied":
                        _logger.Log("Användaren nekade åtkomst.");
                        return null;

                    case "expired_token":
                        _logger.Log("Device code har gått ut. Timeout.");
                        return null;

                    case "":
                        return null;
                    default:
                        throw new Exception($"Okänt OAuth-fel: {error?.Error}");
                }
            }
            catch (JsonException)
            {
                throw new Exception($"Misslyckades tolka felmeddelande från Twitch: {errorBody}");
            }
        }

        _logger.Log("Polling avbröts efter max väntetid utan godkännande.");
        return null;
    }

    // TODO: try catch on bad results
    private async Task<string?> GetUsernameAsync(string accessToken)
    {
        try
        {

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Client-Id", _clientId);

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
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }
        return null;
    }

    public async Task<string> FetchUserId()
    {
        if (AuthResult != null)
        {
            if (AuthResult.UserId != null)
            {
                return AuthResult.UserId;
            }
            if (AuthResult.UserId == null)
            {
                var userId = GetUserIdAsync(AuthResult.AccessToken);
                AuthResult.UserId = userId.Result;
                return AuthResult.UserId;
            }
        }
        else
        {
            throw new Exception("AuthResult was null");
        }
        return "Failure";
    }

    private async Task<string> GetUserIdAsync(string accessToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Client-Id", _clientId);

        var response = await httpClient.GetAsync($"{ApiBaseUrl}/users");
        var responseData = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get UserId. Status: {response.StatusCode}\n{responseData}");
        }

        var json = JsonDocument.Parse(responseData).RootElement;
        var user = json.GetProperty("data")[0];

        // Rätt propertynamn: "id"
        return user.TryGetProperty("id", out var userIdElement)
            ? userIdElement.GetString()
            : throw new Exception("Could not find 'id' in user response.");
    }

    public static void SaveToken(TwitchTokenTokenResponse token)
    {
        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TokenFilePath, json);
    }

    public static TwitchTokenTokenResponse? LoadSavedToken()
    {
        if (!File.Exists(TokenFilePath)) return null;

        try
        {
            var json = File.ReadAllText(TokenFilePath);
            return JsonSerializer.Deserialize<TwitchTokenTokenResponse>(json);
        }
        catch
        {
            return null;
        }
    }
}