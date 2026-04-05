using Avalonia.Threading;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class ChatAggregationService
{
    private readonly ILogService _logService;
    private readonly StateService _stateService;
    private readonly ChatProviderRegistryService _providerRegistry;
    private readonly ChatConcatenationPolicy _concatenationPolicy;
    private readonly ObservableCollection<ChatMessageDto> _messages = [];
    private readonly ObservableCollection<ChatProviderStatusDto> _providerStatuses = [];
    private readonly HashSet<AuthProvider> _connectedProviders = [];
    private readonly Dictionary<AuthProvider, CancellationTokenSource> _providerCancellations = [];
    private readonly ReadOnlyObservableCollection<ChatMessageDto> _readOnlyMessages;
    private readonly ReadOnlyObservableCollection<ChatProviderStatusDto> _readOnlyProviderStatuses;
    private readonly Dictionary<AuthProvider, ChatProviderStatusDto> _providerStatusMap = [];
    private int _syntheticCounter;
    private readonly object _sync = new();

    public ChatAggregationService(
        ILogService logService,
        StateService stateService,
        ChatProviderRegistryService providerRegistry,
        ChatConcatenationPolicy concatenationPolicy)
    {
        _logService = logService;
        _stateService = stateService;
        _providerRegistry = providerRegistry;
        _concatenationPolicy = concatenationPolicy;
        _readOnlyMessages = new ReadOnlyObservableCollection<ChatMessageDto>(_messages);
        _readOnlyProviderStatuses = new ReadOnlyObservableCollection<ChatProviderStatusDto>(_providerStatuses);
    }

    public ReadOnlyObservableCollection<ChatMessageDto> Messages => _readOnlyMessages;
    public ReadOnlyObservableCollection<ChatProviderStatusDto> ProviderStatuses => _readOnlyProviderStatuses;
    public event Action<ChatProviderStatusDto>? ProviderStatusChanged;

    public async Task RefreshConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var targetProviders = ResolveTargetProviders();
        var toConnect = targetProviders.Except(_connectedProviders).ToArray();
        var toDisconnect = _connectedProviders.Except(targetProviders).ToArray();

        foreach (var provider in toDisconnect)
        {
            await DisconnectProviderAsync(provider, cancellationToken);
        }

        foreach (var provider in toConnect)
        {
            try
            {
                await ConnectProviderAsync(provider, cancellationToken);
            }
            catch (Exception ex)
            {
                var status = new ChatProviderStatusDto(
                    provider,
                    false,
                    "Provider connection failed unexpectedly.",
                    ChatProviderRuntimeState.Faulted);

                UpdateProviderStatus(status);
                _logService.Log($"Provider connect isolation for {provider}: {ex.GetType().Name}");
            }
        }
    }

    public void ClearMessages()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Adds a deterministic synthetic chat message for debugging, only when a provider is unavailable at runtime.
    /// </summary>
    public bool TryInjectSyntheticMessage(AuthProvider provider, string seed, out string reason)
    {
        if (!_providerStatusMap.TryGetValue(provider, out var status) ||
            status.State is ChatProviderRuntimeState.Connected or ChatProviderRuntimeState.Connecting)
        {
            reason = "Synthetic injection is disabled while real provider transport is available.";
            return false;
        }

        var safeSeed = string.IsNullOrWhiteSpace(seed) ? "default-seed" : seed.Trim();
        var ordinal = Interlocked.Increment(ref _syntheticCounter);
        var author = $"debug-{provider.ToString().ToLowerInvariant()}";
        var messageText = $"Synthetic({safeSeed}) #{ordinal}";
        var messageId = $"synthetic-{provider}-{safeSeed}-{ordinal}";

        OnChatMessageReceived(new ChatMessageDto(
            provider,
            author,
            messageText,
            DateTime.UtcNow,
            true,
            messageId));

        reason = "Synthetic message injected.";
        return true;
    }

    public void ApplySettings(UserSettings settings)
    {
        if (settings.ChatOverlay.MaxMessages <= 0)
        {
            settings.ChatOverlay.MaxMessages = 200;
        }

        if (settings.ChatOverlay.ConcatenationWindowSeconds <= 0)
        {
            settings.ChatOverlay.ConcatenationWindowSeconds = 8;
        }
    }

    private HashSet<AuthProvider> ResolveTargetProviders()
    {
        var targets = new HashSet<AuthProvider>();
        foreach (var service in _stateService.SelectedServicesToStream.Where(x => x.IsActive))
        {
            if (TryMapServiceToProvider(service, out var provider) && _stateService.AuthObjects.ContainsKey(provider))
            {
                targets.Add(provider);
            }
        }

        return targets;
    }

    private static bool TryMapServiceToProvider(SelectedService service, out AuthProvider provider)
    {
        var name = service.ServiceGroup?.ServiceName ?? service.DisplayName;

        if (name.Contains("Twitch", StringComparison.OrdinalIgnoreCase))
        {
            provider = AuthProvider.Twitch;
            return true;
        }

        if (name.Contains("Kick", StringComparison.OrdinalIgnoreCase))
        {
            provider = AuthProvider.Kick;
            return true;
        }

        if (name.Contains("YouTube", StringComparison.OrdinalIgnoreCase))
        {
            provider = AuthProvider.YouTube;
            return true;
        }

        provider = default;
        return false;
    }

    private async Task ConnectProviderAsync(AuthProvider provider, CancellationToken cancellationToken)
    {
        if (!_providerRegistry.TryGetProvider(provider, out var chatProvider) || chatProvider is null)
        {
            UpdateProviderStatus(new ChatProviderStatusDto(
                provider,
                false,
                "Provider is not registered.",
                ChatProviderRuntimeState.Unavailable));
            return;
        }

        if (_connectedProviders.Contains(provider))
        {
            return;
        }

        chatProvider.ChatMessageReceived += OnChatMessageReceived;
        chatProvider.StatusChanged += OnProviderStatusChanged;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _providerCancellations[provider] = cts;

        try
        {
            await chatProvider.ConnectAsync(cts.Token);
            if (_providerStatusMap.TryGetValue(provider, out var status) &&
                status.State != ChatProviderRuntimeState.Connected)
            {
                chatProvider.ChatMessageReceived -= OnChatMessageReceived;
                chatProvider.StatusChanged -= OnProviderStatusChanged;
                if (_providerCancellations.TryGetValue(provider, out var createdCts))
                {
                    createdCts.Cancel();
                    createdCts.Dispose();
                    _providerCancellations.Remove(provider);
                }
                return;
            }

            _connectedProviders.Add(provider);
        }
        catch (Exception ex)
        {
            chatProvider.ChatMessageReceived -= OnChatMessageReceived;
            chatProvider.StatusChanged -= OnProviderStatusChanged;
            if (_providerCancellations.TryGetValue(provider, out var createdCts))
            {
                createdCts.Cancel();
                createdCts.Dispose();
                _providerCancellations.Remove(provider);
            }

            _logService.Log($"Failed to connect {provider} chat provider: {ex.GetType().Name}");
            UpdateProviderStatus(new ChatProviderStatusDto(
                provider,
                false,
                "Provider transport failed to connect.",
                ChatProviderRuntimeState.Faulted));
        }
    }

    private async Task DisconnectProviderAsync(AuthProvider provider, CancellationToken cancellationToken)
    {
        if (!_providerRegistry.TryGetProvider(provider, out var chatProvider) || chatProvider is null)
        {
            return;
        }

        chatProvider.ChatMessageReceived -= OnChatMessageReceived;
        chatProvider.StatusChanged -= OnProviderStatusChanged;

        if (_providerCancellations.TryGetValue(provider, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _providerCancellations.Remove(provider);
        }

        try
        {
            await chatProvider.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logService.Log($"Failed to disconnect {provider} chat provider: {ex.GetType().Name}");
        }

        _connectedProviders.Remove(provider);
        UpdateProviderStatus(new ChatProviderStatusDto(
            provider,
            false,
            "Disconnected",
            ChatProviderRuntimeState.Disconnected));
    }

    private void OnProviderStatusChanged(ChatProviderStatusDto status)
    {
        UpdateProviderStatus(status);
    }

    private void UpdateProviderStatus(ChatProviderStatusDto status)
    {
        void Apply()
        {
            _providerStatusMap[status.Provider] = status;
            var existingIndex = _providerStatuses
                .Select((x, index) => (x, index))
                .FirstOrDefault(tuple => tuple.x.Provider == status.Provider)
                .index;

            if (existingIndex >= 0 && existingIndex < _providerStatuses.Count)
            {
                _providerStatuses[existingIndex] = status;
            }
            else
            {
                _providerStatuses.Add(status);
            }

            ProviderStatusChanged?.Invoke(status);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
            return;
        }

        Dispatcher.UIThread.Post(Apply);
    }

    private void OnChatMessageReceived(ChatMessageDto incoming)
    {
        void Apply()
        {
            lock (_sync)
            {
                var settings = _stateService.UserSettingsObj.ChatOverlay;
                var maxMessages = settings.MaxMessages > 0 ? settings.MaxMessages : 200;
                if (_messages.Count > 0 && settings.EnableConcatenation)
                {
                    var previous = _messages[^1];
                    var merged = _concatenationPolicy.TryConcatenate(
                        previous,
                        incoming,
                        TimeSpan.FromSeconds(settings.ConcatenationWindowSeconds),
                        settings.MaxConcatenatedLines,
                        out var concatenated);

                    if (merged)
                    {
                        _messages[^1] = concatenated;
                        return;
                    }
                }

                _messages.Add(incoming);
                while (_messages.Count > maxMessages)
                {
                    _messages.RemoveAt(0);
                }
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply();
            return;
        }

        Dispatcher.UIThread.Post(Apply);
    }
}
