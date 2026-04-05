using SSMM_UI.DTO;
using SSMM_UI.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Interfaces;

public interface IChatProvider
{
    AuthProvider Provider { get; }
    event Action<ChatMessageDto>? ChatMessageReceived;
    event Action<ChatProviderStatusDto>? StatusChanged;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}
