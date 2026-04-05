using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders;

public class KickChatProvider : IChatProvider
{
    private readonly ILogService _logService;
    private readonly StateService _stateService;

    public KickChatProvider(ILogService logService, StateService stateService)
    {
        _logService = logService;
        _stateService = stateService;
    }

    public AuthProvider Provider => AuthProvider.Kick;
    public event Action<ChatMessageDto>? ChatMessageReceived;
    public event Action<ChatProviderStatusDto>? StatusChanged;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!_stateService.AuthObjects.TryGetValue(AuthProvider.Kick, out var token) || !token.IsValid)
        {
            const string reason = "No valid Kick token available.";
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Disconnected));
            return Task.CompletedTask;
        }

        const string unavailableReason = "Kick chat transport is not implemented in this build.";
        _logService.Log(unavailableReason);
        _ = ChatMessageReceived;
        StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, unavailableReason, ChatProviderRuntimeState.Unavailable));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _logService.Log("Kick chat provider disconnected.");
        StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Disconnected", ChatProviderRuntimeState.Disconnected));
        return Task.CompletedTask;
    }
}
