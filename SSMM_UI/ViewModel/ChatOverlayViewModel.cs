using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class ChatOverlayViewModel : ObservableObject
{
    private readonly ChatAggregationService _chatAggregationService;
    private readonly StateService _stateService;

    public ChatOverlayViewModel(ChatAggregationService chatAggregationService, StateService stateService)
    {
        _chatAggregationService = chatAggregationService;
        _stateService = stateService;

        ClearMessagesCommand = new RelayCommand(ClearMessages);
        RefreshConnectionsCommand = new AsyncRelayCommand(RefreshConnectionsAsync);
        CloseOverlayCommand = new RelayCommand(RequestCloseOverlay);
        InjectSyntheticMessageCommand = new RelayCommand(InjectSyntheticMessage);
        Messages = _chatAggregationService.Messages;
        ProviderStatuses = _chatAggregationService.ProviderStatuses;
        _chatAggregationService.ProviderStatusChanged += OnProviderStatusChanged;

        ApplySettings();
    }

    public ReadOnlyObservableCollection<ChatMessageDto> Messages { get; }
    public ReadOnlyObservableCollection<ChatProviderStatusDto> ProviderStatuses { get; }

    [ObservableProperty] private bool isOverlayEnabled;
    [ObservableProperty] private bool isAlwaysOnTop;
    [ObservableProperty] private bool isClickThrough;
    [ObservableProperty] private bool enableConcatenation;
    [ObservableProperty] private double overlayOpacity;
    [ObservableProperty] private double fontScale;
    [ObservableProperty] private int maxMessages;
    [ObservableProperty] private int concatenationWindowSeconds;
    [ObservableProperty] private AuthProvider debugInjectionProvider = AuthProvider.Kick;
    [ObservableProperty] private string debugInjectionSeed = "overlay-debug";
    [ObservableProperty] private string? lastDebugInjectionResult;

    public ICommand ClearMessagesCommand { get; }
    public ICommand RefreshConnectionsCommand { get; }
    public ICommand CloseOverlayCommand { get; }
    public ICommand InjectSyntheticMessageCommand { get; }

    public event EventHandler? CloseOverlayRequested;

    public void ApplySettings()
    {
        var settings = _stateService.UserSettingsObj.ChatOverlay;
        IsOverlayEnabled = settings.Enabled;
        IsAlwaysOnTop = settings.IsAlwaysOnTop;
        IsClickThrough = settings.IsClickThrough;
        EnableConcatenation = settings.EnableConcatenation;
        OverlayOpacity = settings.Opacity;
        FontScale = settings.FontScale;
        MaxMessages = settings.MaxMessages;
        ConcatenationWindowSeconds = settings.ConcatenationWindowSeconds;
    }

    public async Task RefreshConnectionsAsync()
    {
        if (!IsOverlayEnabled)
        {
            return;
        }

        await _chatAggregationService.RefreshConnectionsAsync();
    }

    private void ClearMessages()
    {
        _chatAggregationService.ClearMessages();
    }

    private void RequestCloseOverlay()
    {
        CloseOverlayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void InjectSyntheticMessage()
    {
        if (_chatAggregationService.TryInjectSyntheticMessage(DebugInjectionProvider, DebugInjectionSeed, out var reason))
        {
            LastDebugInjectionResult = reason;
            return;
        }

        LastDebugInjectionResult = reason;
    }

    private void OnProviderStatusChanged(ChatProviderStatusDto status)
    {
        if (status.State == ChatProviderRuntimeState.Unavailable && status.Provider != AuthProvider.Twitch)
        {
            DebugInjectionProvider = status.Provider;
        }
    }
}
