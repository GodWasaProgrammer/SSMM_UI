using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Oauth.Google;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders;

public class YouTubeChatProvider : IChatProvider
{
    private static readonly string[] PreferredLifeCycleStatuses = ["live", "testing", "ready", "created"];
    private readonly ILogService _logService;
    private readonly StateService _stateService;
    private YouTubeService? _youtubeService;
    private CancellationTokenSource? _pollLoopCts;
    private Task? _pollLoopTask;
    private string? _liveChatId;
    private string? _nextPageToken;

    public YouTubeChatProvider(ILogService logService, StateService stateService)
    {
        _logService = logService;
        _stateService = stateService;
    }

    public AuthProvider Provider => AuthProvider.YouTube;
    public event Action<ChatMessageDto>? ChatMessageReceived;
    public event Action<ChatProviderStatusDto>? StatusChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!TryGetYouTubeToken(out var token))
        {
            const string reason = "No valid YouTube token available.";
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Disconnected));
            return;
        }

        if (!token.IsValid)
        {
            var refreshed = await TryRefreshAccessTokenAsync(cancellationToken);
            if (!refreshed || !_stateService.AuthObjects.TryGetValue(AuthProvider.YouTube, out token) || !token.IsValid)
            {
                const string reason = "No valid YouTube token available.";
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Disconnected));
                return;
            }
        }

        await ResetSessionAsync(cancellationToken);

        try
        {
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Connecting to YouTube live chat", ChatProviderRuntimeState.Connecting));

            var credential = GoogleCredential.FromAccessToken(token.AccessToken);
            _youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultistreamManager"
            });

            _liveChatId = await DiscoverActiveLiveChatIdAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(_liveChatId))
            {
                const string reason = "No active YouTube live broadcast with chat found.";
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Unavailable));
                await ResetSessionAsync(cancellationToken);
                return;
            }

            _pollLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollLoopTask = Task.Run(() => PollLoopAsync(_pollLoopCts.Token), _pollLoopCts.Token);
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, true, null, ChatProviderRuntimeState.Connected));
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Connection cancelled.", ChatProviderRuntimeState.Disconnected));
        }
        catch (Exception ex)
        {
            _logService.Log($"YouTube chat provider unavailable: {ex.GetType().Name} - {ex.Message}");
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Unable to connect to YouTube chat transport.", ChatProviderRuntimeState.Unavailable));
            await ResetSessionAsync(cancellationToken);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await ResetSessionAsync(cancellationToken);
        _logService.Log("YouTube chat provider disconnected.");
        StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Disconnected", ChatProviderRuntimeState.Disconnected));
    }

    private async Task ResetSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _pollLoopCts?.Cancel();
            if (_pollLoopTask is not null)
            {
                await _pollLoopTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch
        {
            // Best-effort shutdown only.
        }
        finally
        {
            _pollLoopCts?.Dispose();
            _pollLoopCts = null;
            _pollLoopTask = null;
            _nextPageToken = null;
            _liveChatId = null;
            _youtubeService?.Dispose();
            _youtubeService = null;
        }
    }

    private async Task<string?> DiscoverActiveLiveChatIdAsync(
    CancellationToken cancellationToken)
    {
        if (_youtubeService is null)
            return null;

        var request = _youtubeService.LiveBroadcasts.List("snippet,status");
        request.Mine = true;
        request.MaxResults = 50;

        var response = await request.ExecuteAsync(cancellationToken);

        var activeMatch = response.Items?
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Snippet?.LiveChatId) &&
                item.Status?.LifeCycleStatus is "live" or "testing" or "ready" or "created")
            .OrderBy(item =>
                item.Status?.LifeCycleStatus == "live" ? 0 : 1)
            .Select(item => item.Snippet!.LiveChatId)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(activeMatch))
            return activeMatch;

        _logService.Log(
            "YouTube chat provider: no active broadcast with liveChatId found.");

        return null;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        if (_youtubeService is null || string.IsNullOrWhiteSpace(_liveChatId))
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = _youtubeService.LiveChatMessages.List(_liveChatId, "snippet,authorDetails");
                request.PageToken = _nextPageToken;
                var response = await request.ExecuteAsync(cancellationToken);

                foreach (var item in response.Items)
                {
                    var author = item.AuthorDetails?.DisplayName;
                    if (string.IsNullOrWhiteSpace(author))
                    {
                        author = "YouTubeUser";
                    }

                    var message = item.Snippet?.DisplayMessage;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    var publishedAt = DateTime.UtcNow;
                    var publishedAtRaw = item.Snippet?.PublishedAtRaw;
                    if (!string.IsNullOrWhiteSpace(publishedAtRaw) &&
                        DateTime.TryParse(publishedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
                    {
                        publishedAt = parsed.ToUniversalTime();
                    }

                    ChatMessageReceived?.Invoke(new ChatMessageDto(
                        AuthProvider.YouTube,
                        author,
                        message,
                        publishedAt,
                        false,
                        item.Id ?? $"youtube-{Math.Abs(HashCode.Combine(author, message))}"));
                }

                _nextPageToken = response.NextPageToken;
                var pollMs = response.PollingIntervalMillis ?? 5000;
                var delayMs = (int)Math.Clamp(pollMs, 1000L, 60000L);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (GoogleApiException gex) when (IsAuthFailure(gex))
            {
                _logService.Log($"YouTube chat auth expired: {(int)gex.HttpStatusCode}. Attempting refresh.");
                var refreshed = await TryRefreshAccessTokenAsync(cancellationToken);
                if (refreshed)
                {
                    StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, true, "YouTube session refreshed.", ChatProviderRuntimeState.Connected));
                    continue;
                }

                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "YouTube authorization expired. Please log in again.", ChatProviderRuntimeState.Unavailable));
                break;
            }
            catch (GoogleApiException gex) when (IsLiveChatUnavailable(gex))
            {
                _logService.Log($"YouTube chat stream changed ({(int)gex.HttpStatusCode}). Attempting live chat rediscovery.");
                _liveChatId = await DiscoverActiveLiveChatIdAsync(cancellationToken);
                _nextPageToken = null;
                if (!string.IsNullOrWhiteSpace(_liveChatId))
                {
                    StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, true, "Recovered live chat session.", ChatProviderRuntimeState.Connected));
                    continue;
                }

                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "No active YouTube live chat found.", ChatProviderRuntimeState.Unavailable));
                break;
            }
            catch (GoogleApiException gex)
            {
                _logService.Log($"YouTube chat API faulted ({(int)gex.HttpStatusCode}): {string.Join(",", GetApiErrorReasons(gex))}");
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "YouTube chat API faulted.", ChatProviderRuntimeState.Faulted));
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.Log($"YouTube chat read loop faulted: {ex.GetType().Name}");
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "YouTube chat read loop faulted.", ChatProviderRuntimeState.Faulted));
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task<bool> TryRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var googleToken = ResolveGoogleTokenFromStateOrStorage();
        if (googleToken is null || string.IsNullOrWhiteSpace(googleToken.RefreshToken))
        {
            return false;
        }

        try
        {
            var authService = new GoogleAuthService(_logService, _stateService);
            var refreshed = await authService.RefreshTokenAsync(googleToken.RefreshToken);
            if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(refreshed.Username))
            {
                refreshed.Username = googleToken.Username;
            }

            _stateService.SerializeToken(AuthProvider.YouTube, refreshed);

            _youtubeService?.Dispose();
            var credential = GoogleCredential.FromAccessToken(refreshed.AccessToken);
            _youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MultistreamManager"
            });

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logService.Log($"YouTube token refresh failed: {ex.GetType().Name}");
            return false;
        }
    }

    private bool TryGetYouTubeToken(out IAuthToken token)
    {
        token = null!;
        var googleToken = ResolveGoogleTokenFromStateOrStorage();
        if (googleToken is null)
        {
            return false;
        }

        token = googleToken;
        return true;
    }

    private GoogleToken? ResolveGoogleTokenFromStateOrStorage()
    {
        if (_stateService.AuthObjects.TryGetValue(AuthProvider.YouTube, out var existing) &&
            existing is GoogleToken fromState)
        {
            return fromState;
        }

        return _stateService.DeserializeToken<GoogleToken>(AuthProvider.YouTube);
    }

    private static bool IsAuthFailure(GoogleApiException gex)
    {
        var status = (int)gex.HttpStatusCode;
        return status == 401 || status == 403;
    }

    private static bool IsLiveChatUnavailable(GoogleApiException gex)
    {
        var status = (int)gex.HttpStatusCode;
        if (status != 403 && status != 404)
        {
            return false;
        }

        var reasons = GetApiErrorReasons(gex);
        return reasons.Any(x =>
            x.Equals("liveChatEnded", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("liveChatDisabled", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("liveChatNotFound", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("notFound", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetApiErrorReasons(GoogleApiException gex)
    {
        if (gex.Error?.Errors is null || gex.Error.Errors.Count == 0)
        {
            return [];
        }

        return [.. gex.Error.Errors
            .Select(x => x.Reason)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)!];
    }
}
