using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3;
using SSMM_UI.Interfaces;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Collections.ObjectModel;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class LeftSideBarViewModel : ObservableObject
{
    public LeftSideBarViewModel(IDialogService dialogService, 
                                VideoPlayerService vidPlayer,
                                ILogService logService, 
                                StateService stateService)
    {

        // ==== Stream Inspection window ====
        ToggleReceivingStreamCommand = new RelayCommand(ToggleReceivingStream);

        // ==== Selected Services controls ====
        AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected!);
        RemoveSelectedServiceCommand = new RelayCommand(RemoveSelectedService);

        // ==== Service assignment ====
        _dialogService = dialogService;
        _videoPlayerService = vidPlayer;
        _logService = logService;
        _stateService = stateService;
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;

        // Get State
        _userSettings = _stateService.UserSettingsObj;
    }


    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];


    // == Internal stream inspection toggle ==
    public ICommand ToggleReceivingStreamCommand { get; }
    // == Service Selections ==
    public IAsyncRelayCommand<RtmpServiceGroup> AddServiceCommand { get; }
    public ICommand RemoveSelectedServiceCommand { get; }


    // ==== Services ====
    private readonly VideoPlayerService _videoPlayerService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;
    private readonly StateService _stateService;

    // == bool toggler for the preview window for stream ==
    [ObservableProperty] private bool isReceivingStream;

    [ObservableProperty] private RtmpServiceGroup? selectedRtmpService;
    async partial void OnSelectedRtmpServiceChanged(RtmpServiceGroup? value)
    {
        if(value != null)
        await OnRTMPServiceSelected(value);
        SelectedRtmpService = null;
    }

    // Settings
    private readonly UserSettings? _userSettings;

    // ==== Service Selections ====
    [ObservableProperty]
    private SelectedService? _selectedService;

    private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
    {
        var result = await _dialogService.ShowServerDetailsAsync(group);

        if (!result)
            _logService.Log($"Cancelled adding service: {group.ServiceName}\n");
    }

    [ObservableProperty] private string? streamButtonText = "Start Receiving";
    private void ToggleReceivingStream()
    {
        IsReceivingStream = !IsReceivingStream;
        _videoPlayerService.ToggleVisibility(IsReceivingStream);
        StreamButtonText = IsReceivingStream ? "Stop Receiving" : "Start Receiving";
    }

    async partial void OnSelectedServiceChanged(SelectedService? value)
    {
        if (value is null)
            return;

        await _dialogService.InspectSelectedService(value);
        SelectedService = null;
    }

    private void RemoveSelectedService()
    {
        if (SelectedService == null)
        {
            _logService.Log("No Service selected for removal");
            return;
        }

        if (!SelectedServicesToStream.Contains(SelectedService))
        {
            _logService.Log("The selected service doesnt exist in the list");
            return;
        }

        var serviceName = SelectedService.DisplayName;
        SelectedServicesToStream.Remove(SelectedService);
        _logService.Log($"Removed Service: {serviceName}");
        SelectedService = null;
    }
}