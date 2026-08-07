using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Services.ChatProviders.ChatClients;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders;

public class KickChatProvider : IChatProvider
{
    private readonly ILogService _logService;
    private readonly StateService _stateService;
    private readonly KickChatClient _chatClient;
    private Task? _connectionTask;

    public KickChatProvider(
    ILogService logService,
    StateService stateService,
    KickChatClient chatClient)
    {
        _logService = logService;
        _stateService = stateService;
        _chatClient = chatClient;

        _chatClient.MessageReceived += message =>
        {
            ChatMessageReceived?.Invoke(message);
        };
    }

    public AuthProvider Provider => AuthProvider.Kick;
    public event Action<ChatMessageDto>? ChatMessageReceived;
    public event Action<ChatProviderStatusDto>? StatusChanged;

    public async Task ConnectAsync(
    CancellationToken cancellationToken)
    {
        if (!_stateService.AuthObjects.TryGetValue(
            AuthProvider.Kick,
            out var token))
        {
            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "No Kick authentication context.",
                    ChatProviderRuntimeState.Disconnected));

            return;
        }


        var channelName = token.Username;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    "Kick channel missing.",
                    ChatProviderRuntimeState.Unavailable));

            return;
        }


        StatusChanged?.Invoke(
            new ChatProviderStatusDto(
                Provider,
                false,
                "Connecting to Kick chat",
                ChatProviderRuntimeState.Connecting));


        try
        {
            await _chatClient.ConnectAsync(
                channelName,
                cancellationToken);


            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    true,
                    null,
                    ChatProviderRuntimeState.Connected));
            _logService.Log($"Kick Chat Provider connected for channel {channelName}");
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"Kick connection failed: {ex.Message}");

            StatusChanged?.Invoke(
                new ChatProviderStatusDto(
                    Provider,
                    false,
                    ex.Message,
                    ChatProviderRuntimeState.Faulted));
        }
    }


    public async Task DisconnectAsync(
        CancellationToken cancellationToken)
    {
        await _chatClient.DisconnectAsync(
            cancellationToken);


        StatusChanged?.Invoke(
            new ChatProviderStatusDto(
                Provider,
                false,
                "Disconnected",
                ChatProviderRuntimeState.Disconnected));


        _logService.Log(
            "Kick chat provider disconnected.");
    }
}
