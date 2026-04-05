namespace SSMM_UI.Enums;

/// <summary>
/// Represents runtime connectivity and availability state for a chat provider.
/// </summary>
public enum ChatProviderRuntimeState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Unavailable = 3,
    Faulted = 4
}
