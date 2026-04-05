using SSMM_UI.Enums;

namespace SSMM_UI.DTO;

public sealed record ChatProviderStatusDto(
    AuthProvider Provider,
    bool IsConnected,
    string? Reason,
    ChatProviderRuntimeState State = ChatProviderRuntimeState.Disconnected);
