using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Interfaces;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SSMM_UI.ViewModel;

public partial class LeftSideBarViewModel : ObservableObject
{
    public LeftSideBarViewModel(IDialogService dialogService,
                                ILogService logService, 
                                StateService stateService)
    {
        // ==== Selected Services controls ====
        AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected!);

        // ==== Service assignment ====
        _dialogService = dialogService;
        _logService = logService;
        _stateService = stateService;
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;
    }

    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];

    // == Service Selections ==
    public IAsyncRelayCommand<RtmpServiceGroup> AddServiceCommand { get; }
    
    // ==== Services ====
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;
    private readonly StateService _stateService;

    [ObservableProperty] private RtmpServiceGroup? selectedRtmpService;
    async partial void OnSelectedRtmpServiceChanged(RtmpServiceGroup? value)
    {
        if(value != null)
        await OnRTMPServiceSelected(value);
        SelectedRtmpService = null;
    }
    private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
    {
        var result = await _dialogService.ShowServerDetailsAsync(group);

        if (!result)
            _logService.Log($"Cancelled adding service: {group.ServiceName}\n");
    }
    [ObservableProperty] SelectedService? _selectedService;
    async partial void OnSelectedServiceChanged(SelectedService? value)
    {
        if (value is null)
            return;

        await _dialogService.InspectSelectedService(value);
        SelectedService = null;
    }
}