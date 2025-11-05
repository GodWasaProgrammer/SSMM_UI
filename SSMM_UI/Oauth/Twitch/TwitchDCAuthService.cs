using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Oauth.Twitch;

public class TwitchDCAuthService : IOAuthService<TwitchToken>
{
    private readonly HttpClient _httpClient;
    private const string DcfApiAdress = "https://id.twitch.tv/oauth2/device";
    private const string TokenAdress = "https://id.twitch.tv/oauth2/token";
    public readonly string _clientId = "y1cd8maguk5ob1m3lwvhdtupbj6pm3";
    private const string ApiBaseUrl = "https://api.twitch.tv/helix";
    private readonly ILogService _logger;
    private readonly StateService _stateService;
    public TwitchDCAuthService(ILogService logger, StateService stateService)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _stateService = stateService;
    }

    private TwitchToken? _authResult;
    public TwitchToken? AuthResult
    {
        get => _authResult;
        set
        {
            _authResult = value;
            if (OnAccessTokenUpdated != null && value != null)
            {
                OnAccessTokenUpdated.Invoke(value.AccessToken);
            }
        }
    }

    public delegate void AccessTokenUpdatedDelegate(string token);
    public AccessTokenUpdatedDelegate? OnAccessTokenUpdated;

    readonly string[] scopes =
        [
            TwitchScopes.UserReadEmail,
            TwitchScopes.ChannelManageBroadcast,
            TwitchScopes.StreamKey
        ];

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

    public async Task<TwitchToken?> LoginAsync()
    {
        // check if there is a token
        var token = await TryUseExistingTokenAsync();
        if (token != null)
        {
            if (token.IsValid)
            {
                AuthResult = token;
                return token;
            }
            if (!token.IsValid)
            {
                if (!string.IsNullOrEmpty(token.RefreshToken))
                {
                    var res = await RefreshTokenAsync(token.RefreshToken);
                    if (res != null)
                    {
                        token = res;
                        _stateService.SerializeToken(OAuthServices.Twitch, token);
                        AuthResult = token;
                        return token;
                    }
                }
            }

        }
        // otherwise we continue with normal login
        HttpRequestMessage request = UriCreator();

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TwitchDCResponse>();

        if (result != null)
        {
            BrowserHelper.OpenUrlInBrowser(result.VerificationUri);

            token = await PollForTokenAsync(result.DeviceCode, result.Interval);

            if (token != null)
            {
                return token;
            }
            else
            {
                _logger.Log("Timeout - user did not log in");
            }
        }
        // login failed.
        return null;
    }

    private HttpRequestMessage UriCreator()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DcfApiAdress);

        var content = new FormUrlEncodedContent(
        [
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("scope", string.Join(" ", scopes))
                ]);

        request.Content = content;
        return request;
    }

    public async Task<TwitchToken?> RefreshTokenAsync(string refreshToken)
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
            var errorToken = new TwitchToken
            {
                ErrorMessage = $"⚠️ Refresh failed: {response.StatusCode}\n{responseBody}"
            };
            // since the refresh failed, the token nor its refreshtoken is no longer valid, delete local token to force full relog

            // TODO : This seems like a shitty designchoice, need to revisit
            return errorToken;
        }

        var token = JsonSerializer.Deserialize<TwitchToken>(responseBody);
        if (token is not null)
        {
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            token.Username = await GetUsernameAsync(token.AccessToken);
            token.UserId = await GetUserIdAsync(token.AccessToken);
            _stateService.SerializeToken(OAuthServices.Twitch, token);
        }
        AuthResult = token;
        return token!;
    }

    public async Task<TwitchToken?> TryUseExistingTokenAsync()
    {
        var token = _stateService.DeserializeToken<TwitchToken>(OAuthServices.Twitch);
        if (token != null)
        {
            if (token.IsValid)
            {
                var duser = await GetUsernameAsync(token.AccessToken);
                token.Username = duser;
                AuthResult = token;
                return token;
            }
            else
            {
                // try to refresh
                var res = await RefreshTokenAsync(token.RefreshToken);
                if (res != null)
                {
                    // refresh was successful, return
                    token = res;
                    _stateService.SerializeToken(OAuthServices.Twitch, token);
                    AuthResult = token;
                    return token;
                }
            }

        }
        return null;
    }

    public async Task<TwitchToken?> PollForTokenAsync(string deviceCode, int intervalSeconds, int maxWaitSeconds = 300)
    {
        int elapsed = 0;

        await Task.Delay(5000);

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
                var token = await response.Content.ReadFromJsonAsync<TwitchToken>();

                if (token != null)
                {
                    token.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                    token.Username = await GetUsernameAsync(token.AccessToken);
                    token.UserId = await GetUserIdAsync(token.AccessToken);
                    AuthResult = token;
                    _stateService.SerializeToken(OAuthServices.Twitch, token);
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
                        _logger.Log("Twitch says we are polling too quick, raising interval...");
                        continue;

                    case "access_denied":
                        _logger.Log("User was denied access.");
                        return null;

                    case "expired_token":
                        _logger.Log("Device code is old. Timeout.");
                        return null;

                    case "":
                        return null;
                    default:
                        throw new Exception($"Unknown OAuth-error: {error?.Error}");
                }
            }
            catch (JsonException)
            {
                throw new Exception($"Failed to parse error message from Twitch: {errorBody}");
            }
        }

        _logger.Log("Polling was cancelled after max timeout window with no approval.");
        return null;
    }

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

    public string FetchUserId()
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
                if (AuthResult.UserId != null)
                    return AuthResult.UserId;
            }
        }
        else
        {
            throw new Exception("AuthResult was null");
        }
        return "Failure";
    }

    private async Task<string?> GetUserIdAsync(string accessToken)
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
}