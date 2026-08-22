using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Grpc.Core;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Services.ChatProviders.ChatClients;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders;

public class YouTubeChatProvider : IChatProvider
{
    private static readonly string[] PreferredLifeCycleStatuses =
        ["live", "testing", "ready", "created"];

    private readonly ILogService _logService;
    private readonly StateService _stateService;

    private YouTubeService? _youtubeService;
    private YoutubeChatClient? _chatClient;

    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;

    private string? _liveChatId;

    public YouTubeChatProvider(
        ILogService logService,
        StateService stateService)
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

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    reason,
                    ChatProviderRuntimeState.Disconnected));

            return;
        }

        if (!token.IsValid)
        {
            var refreshed = await TryRefreshAccessTokenAsync(
                cancellationToken);

            if (!refreshed ||
                !_stateService.AuthObjects.TryGetValue(
                    AuthProvider.YouTube,
                    out token) ||
                !token.IsValid)
            {
                const string reason = "No valid YouTube token available.";

                StatusChanged?.Invoke(
                    new ChatProviderStatusDto(
                        Provider,
                        false,
                        reason,
                        ChatProviderRuntimeState.Disconnected));

                return;
            }
        }

        await ResetSessionAsync(cancellationToken);

        try
        {
            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "Connecting to YouTube live chat",
                    ChatProviderRuntimeState.Connecting));

            var credential = GoogleCredential.FromAccessToken(
                token.AccessToken);

            _youtubeService = new YouTubeService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MultistreamManager"
                });

            _liveChatId = await DiscoverActiveLiveChatIdAsync(
                cancellationToken);

            if (string.IsNullOrWhiteSpace(_liveChatId))
            {
                const string reason =
                    "No active YouTube live broadcast with chat found.";

                StatusChanged?.Invoke(
                    new ChatProviderStatusDto(
                        Provider,
                        false,
                        reason,
                        ChatProviderRuntimeState.Unavailable));

                await ResetSessionAsync(cancellationToken);
                return;
            }

            _chatClient = new YoutubeChatClient(_liveChatId);

            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

            _streamTask = Task.Run(
                () => StreamLoopAsync(
                    token.AccessToken,
                    _streamCts.Token),
                _streamCts.Token);

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    true,
                    null,
                    ChatProviderRuntimeState.Connected));
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "Connection cancelled.",
                    ChatProviderRuntimeState.Disconnected));

            await ResetSessionAsync(
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"YouTube chat provider unavailable: " +
                $"{ex.GetType().Name} - {ex.Message}");

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "Unable to connect to YouTube chat transport.",
                    ChatProviderRuntimeState.Unavailable));

            await ResetSessionAsync(
                CancellationToken.None);
        }
    }

    public async Task DisconnectAsync(
        CancellationToken cancellationToken)
    {
        await ResetSessionAsync(cancellationToken);

        _logService.Log(
            "YouTube chat provider disconnected.");

        StatusChanged?.Invoke(
            new ChatProviderStatusDto(
                Provider,
                false,
                "Disconnected",
                ChatProviderRuntimeState.Disconnected));
    }

    private async Task ResetSessionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _streamCts?.Cancel();

            if (_streamTask is not null)
            {
                await _streamTask.WaitAsync(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);
            }
        }
        catch
        {
            // Best-effort shutdown only.
        }
        finally
        {
            _streamCts?.Dispose();
            _streamCts = null;

            _streamTask = null;

            _chatClient?.Dispose();
            _chatClient = null;

            _liveChatId = null;

            _youtubeService?.Dispose();
            _youtubeService = null;
        }
    }

    private async Task StreamLoopAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (_chatClient is null)
        {
            return;
        }

        try
        {
            _logService.Log($"YouTube chat gRPC stream connected for liveChatId {_liveChatId}.");

            await foreach (var message in _chatClient.StreamAsync(
                accessToken,
                cancellationToken))
            {
                var author =
                    message.AuthorDetails?.DisplayName;

                if (string.IsNullOrWhiteSpace(author))
                {
                    author = "YouTubeUser";
                }

                var text =
                    message.Snippet?.DisplayMessage;

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var publishedAt = DateTime.UtcNow;

                if (DateTime.TryParse(
                    message.Snippet?.PublishedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal,
                    out var parsed))
                {
                    publishedAt = parsed.ToUniversalTime();
                }

                var messageId = string.IsNullOrWhiteSpace(message.Id)
                    ? $"youtube-{Math.Abs(
                        HashCode.Combine(author, text))}"
                    : message.Id;

                ChatMessageReceived?.Invoke(
                    new ChatMessageDto(
                        AuthProvider.YouTube,
                        author,
                        text,
                        publishedAt,
                        false,
                        messageId));
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (RpcException ex)
        {
            _logService.Log(
                $"YouTube chat gRPC stream faulted: " +
                $"{ex.StatusCode} - {ex.Status.Detail}");

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "YouTube chat stream faulted.",
                    ChatProviderRuntimeState.Faulted));
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"YouTube chat stream faulted: " +
                $"{ex.GetType().Name} - {ex.Message}");

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "YouTube chat stream faulted.",
                    ChatProviderRuntimeState.Faulted));
        }
    }

    private async Task<string?> DiscoverActiveLiveChatIdAsync(
        CancellationToken cancellationToken)
    {
        if (_youtubeService is null)
        {
            return null;
        }

        var request = _youtubeService.LiveBroadcasts.List(
            "snippet,status");

        request.Mine = true;
        request.MaxResults = 50;

        var response = await request.ExecuteAsync(
            cancellationToken);

        var activeMatch = response.Items?
            .Where(item =>
                !string.IsNullOrWhiteSpace(
                    item.Snippet?.LiveChatId) &&
                item.Status?.LifeCycleStatus is not null &&
                PreferredLifeCycleStatuses.Contains(
                    item.Status.LifeCycleStatus,
                    StringComparer.OrdinalIgnoreCase))
            .OrderBy(item =>
                item.Status?.LifeCycleStatus
                    ?.Equals(
                        "live",
                        StringComparison.OrdinalIgnoreCase) == true
                    ? 0
                    : 1)
            .Select(item =>
                item.Snippet!.LiveChatId)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(activeMatch))
        {
            return activeMatch;
        }

        _logService.Log(
            "YouTube chat provider: no active broadcast " +
            "with liveChatId found.");

        return null;
    }

    private async Task<bool> TryRefreshAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        var googleToken =
            ResolveGoogleTokenFromStateOrStorage();

        if (googleToken is null ||
            string.IsNullOrWhiteSpace(googleToken.RefreshToken))
        {
            return false;
        }

        try
        {
            var authService = new GoogleAuthService(
                _logService,
                _stateService);

            var refreshed =
                await authService.RefreshTokenAsync(
                    googleToken.RefreshToken);

            if (refreshed is null ||
                string.IsNullOrWhiteSpace(
                    refreshed.AccessToken))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                refreshed.Username))
            {
                refreshed.Username =
                    googleToken.Username;
            }

            _stateService.SerializeToken(
                AuthProvider.YouTube,
                refreshed);

            _youtubeService?.Dispose();

            var credential =
                GoogleCredential.FromAccessToken(
                    refreshed.AccessToken);

            _youtubeService = new YouTubeService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MultistreamManager"
                });

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"YouTube token refresh failed: " +
                $"{ex.GetType().Name}");

            return false;
        }
    }

    private bool TryGetYouTubeToken(
        out IAuthToken token)
    {
        token = null!;

        var googleToken =
            ResolveGoogleTokenFromStateOrStorage();

        if (googleToken is null)
        {
            return false;
        }

        token = googleToken;

        return true;
    }

    private GoogleToken? ResolveGoogleTokenFromStateOrStorage()
    {
        if (_stateService.AuthObjects.TryGetValue(
                AuthProvider.YouTube,
                out var existing) &&
            existing is GoogleToken fromState)
        {
            return fromState;
        }

        return _stateService.DeserializeToken<GoogleToken>(
            AuthProvider.YouTube);
    }
}